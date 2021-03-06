﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BitcoinNet.BuilderExtensions;
using BitcoinNet.Crypto;
using BitcoinNet.Policy;
using BitcoinNet.Protocol;
using BitcoinNet.Scripting;
using Builder = System.Func<BitcoinNet.TransactionBuilder.TransactionBuildingContext, BitcoinNet.IMoney>;

namespace BitcoinNet
{
	[Flags]
	public enum ChangeType
	{
		All = 3,
		Colored = 1,
		Uncolored = 2
	}

	public interface ICoinSelector
	{
		IEnumerable<ICoin> Select(IEnumerable<ICoin> coins, IMoney target);
	}

	/// <summary>
	///     Algorithm implemented by bitcoin core https://github.com/bitcoin/bitcoin/blob/master/src/wallet.cpp#L1276
	///     Minimize the change
	/// </summary>
	public class DefaultCoinSelector : ICoinSelector
	{
		private readonly Random _rand = new Random();

		public DefaultCoinSelector()
		{
		}

		public DefaultCoinSelector(int seed)
		{
			_rand = new Random(seed);
		}

		public DefaultCoinSelector(Random random)
		{
			_rand = random;
		}

		/// <summary>
		///     Select all coins belonging to same scriptPubKey together to protect privacy. (Default: true)
		/// </summary>
		public bool GroupByScriptPubKey { get; set; } = true;

		// ICoinSelector Members

		public IEnumerable<ICoin> Select(IEnumerable<ICoin> coins, IMoney target)
		{
			var zero = target.Sub(target);

			var result = new List<ICoin>();
			var total = zero;

			if (target.CompareTo(zero) == 0)
			{
				return result;
			}

			var orderedCoinGroups = coins
				.GroupBy(c => GroupByScriptPubKey ? c.TxOut.ScriptPubKey : new Key().ScriptPubKey)
				.Select(scriptPubKeyCoins => new
				{
					Amount = scriptPubKeyCoins.Select(c => c.Amount).Sum(zero),
					Coins = scriptPubKeyCoins.ToList()
				}).OrderBy(c => c.Amount);


			var targetCoin = orderedCoinGroups
				.FirstOrDefault(c => c.Amount.CompareTo(target) == 0);
			//If any of your UTXO² matches the Target¹ it will be used.
			if (targetCoin != null)
			{
				return targetCoin.Coins;
			}

			foreach (var coinGroup in orderedCoinGroups)
			{
				if (coinGroup.Amount.CompareTo(target) == -1 && total.CompareTo(target) == -1)
				{
					total = total.Add(coinGroup.Amount);
					result.AddRange(coinGroup.Coins);
					//If the "sum of all your UTXO smaller than the Target" happens to match the Target, they will be used. (This is the case if you sweep a complete wallet.)
					if (total.CompareTo(target) == 0)
					{
						return result;
					}
				}
				else
				{
					if (total.CompareTo(target) == -1 && coinGroup.Amount.CompareTo(target) == 1)
					{
						//If the "sum of all your UTXO smaller than the Target" doesn't surpass the target, the smallest UTXO greater than your Target will be used.
						return coinGroup.Coins;
					}

					//						Else Bitcoin Core does 1000 rounds of randomly combining unspent transaction outputs until their sum is greater than or equal to the Target. If it happens to find an exact match, it stops early and uses that.
					//Otherwise it finally settles for the minimum of
					//the smallest UTXO greater than the Target
					//the smallest combination of UTXO it discovered in Step 4.
					var allCoins = orderedCoinGroups.ToArray();
					IMoney minTotal = null;
					for (var _ = 0; _ < 1000; _++)
					{
						var selection = new List<ICoin>();
						Utils.Shuffle(allCoins, _rand);
						total = zero;
						for (var i = 0; i < allCoins.Length; i++)
						{
							selection.AddRange(allCoins[i].Coins);
							total = total.Add(allCoins[i].Amount);
							if (total.CompareTo(target) == 0)
							{
								return selection;
							}

							if (total.CompareTo(target) == 1)
							{
								break;
							}
						}

						if (total.CompareTo(target) == -1)
						{
							return null;
						}

						if (minTotal == null || total.CompareTo(minTotal) == -1)
						{
							minTotal = total;
						}
					}
				}
			}

			if (total.CompareTo(target) == -1)
			{
				return null;
			}

			return result;
		}
	}

	/// <summary>
	///     Exception thrown when not enough funds are present for verifying or building a transaction
	/// </summary>
	public class NotEnoughFundsException : Exception
	{
		public NotEnoughFundsException(string message, string group, IMoney missing)
			: base(BuildMessage(message, group, missing))
		{
			Missing = missing;
			Group = group;
		}

		public NotEnoughFundsException(string message, Exception inner)
			: base(message, inner)
		{
		}

		/// <summary>
		///     The group name who is missing the funds
		/// </summary>
		public string Group { get; set; }

		/// <summary>
		///     Amount of Money missing
		/// </summary>
		public IMoney Missing { get; set; }

		private static string BuildMessage(string message, string group, IMoney missing)
		{
			var builder = new StringBuilder();
			builder.Append(message);
			if (group != null)
			{
				builder.Append(" in group " + group);
			}

			if (missing != null)
			{
				builder.Append(" with missing amount " + missing);
			}

			return builder.ToString();
		}
	}

	/// <summary>
	///     A class for building and signing all sort of transactions easily
	///     (http://www.codeproject.com/Articles/835098/NBitcoin-Build-Them-All)
	/// </summary>
	public class TransactionBuilder
	{
		private static readonly TxNullDataTemplate OpReturnTemplate = new TxNullDataTemplate(1024 * 1024);
		private readonly List<BuilderGroup> _builderGroups = new List<BuilderGroup>();
		private readonly List<Key> _keys = new List<Key>();

		private readonly List<Tuple<PubKey, ECDSASignature>> _knownSignatures =
			new List<Tuple<PubKey, ECDSASignature>>();

		private readonly Dictionary<Script, Script> _scriptPubKeyToRedeem = new Dictionary<Script, Script>();
		private bool _built;
		private Transaction _completedTransaction;
		private BuilderGroup _currentGroup;
		private SendBuilder _lastSendBuilder;
		private LockTime? _lockTime;
		private string _opReturnUser;
		private SendBuilder _substractFeeBuilder;
		private Money _totalFee = Money.Zero;

		[Obsolete("Use Network.CreateTransactionBuilder() or ConsensusFactory.CreateTransactionBuilder() instead")]
		public TransactionBuilder()
		{
			ShuffleRandom = new Random();
			CoinSelector = new DefaultCoinSelector(ShuffleRandom);
			StandardTransactionPolicy = new StandardTransactionPolicy();
			DustPrevention = true;
			InitExtensions();
		}

		[Obsolete(
			"Use Network.CreateTransactionBuilder(int seed) or ConsensusFactory.CreateTransactionBuilder(int seed) instead")]
		public TransactionBuilder(int seed)
		{
			ShuffleRandom = new Random(seed);
			CoinSelector = new DefaultCoinSelector(ShuffleRandom);
			StandardTransactionPolicy = new StandardTransactionPolicy();
			DustPrevention = true;
			InitExtensions();
		}

		internal BuilderGroup CurrentGroup
		{
			get
			{
				if (_currentGroup == null)
				{
					_currentGroup = new BuilderGroup(this);
					_builderGroups.Add(_currentGroup);
				}

				return _currentGroup;
			}
		}

		/// <summary>
		///     The random number generator used for shuffling transaction outputs or selected coins
		/// </summary>
		public Random ShuffleRandom { get; set; } = new Random();

		public ICoinSelector CoinSelector { get; set; }

		/// <summary>
		///     If true, it will remove any TxOut below Dust, so the transaction get correctly relayed by the network. (Default:
		///     true)
		/// </summary>
		public bool DustPrevention { get; set; }

		/// <summary>
		///     If true and the transaction has two outputs sending to the same scriptPubKey, those will be merged into a single
		///     output. (Default: true)
		/// </summary>
		public bool MergeOutputs { get; set; } = true;

		/// <summary>
		///     If true, the TransactionBuilder will not select coins whose fee to spend is higher than its value. (Default: true)
		///     The cost of spending a coin is based on the <see cref="FilterUneconomicalCoinsRate" />.
		/// </summary>
		public bool FilterUneconomicalCoins { get; set; } = true;

		/// <summary>
		///     If <see cref="FilterUneconomicalCoins" /> is true, this rate is used to know if an output is economical.
		///     This property is set automatically when calling <see cref="SendEstimatedFees(FeeRate)" /> or
		///     <see cref="SendEstimatedFeesSplit(FeeRate)" />.
		/// </summary>
		public FeeRate FilterUneconomicalCoinsRate { get; set; }

		/// <summary>
		///     A callback used by the TransactionBuilder when it does not find the coin for an input
		/// </summary>
		public Func<OutPoint, ICoin> CoinFinder { get; set; }

		/// <summary>
		///     A callback used by the TransactionBuilder when it does not find the key for a scriptPubKey
		/// </summary>
		public Func<Script, Key> KeyFinder { get; set; }

		public StandardTransactionPolicy StandardTransactionPolicy { get; set; }

		public ConsensusFactory ConsensusFactory { get; private set; } = Network.Main.Consensus.ConsensusFactory;

		public List<BuilderExtension> Extensions { get; } = new List<BuilderExtension>();

		private void InitExtensions()
		{
			Extensions.Add(new P2PKHBuilderExtension());
			Extensions.Add(new P2MultiSigBuilderExtension());
			Extensions.Add(new P2PKBuilderExtension());
			Extensions.Add(new OPTrueExtension());
		}

		public TransactionBuilder SetLockTime(LockTime lockTime)
		{
			_lockTime = lockTime;
			return this;
		}

		public TransactionBuilder AddKeys(params ISecret[] keys)
		{
			AddKeys(keys.Select(k => k.PrivateKey).ToArray());
			return this;
		}

		public TransactionBuilder AddKeys(params Key[] keys)
		{
			_keys.AddRange(keys);
			foreach (var k in keys)
			{
				AddKnownRedeems(k.PubKey.ScriptPubKey);
				AddKnownRedeems(k.PubKey.Hash.ScriptPubKey);
			}

			return this;
		}

		public TransactionBuilder AddKnownSignature(PubKey pubKey, TransactionSignature signature)
		{
			if (pubKey == null)
			{
				throw new ArgumentNullException(nameof(pubKey));
			}

			if (signature == null)
			{
				throw new ArgumentNullException(nameof(signature));
			}

			_knownSignatures.Add(Tuple.Create(pubKey, signature.Signature));
			return this;
		}

		public TransactionBuilder AddKnownSignature(PubKey pubKey, ECDSASignature signature)
		{
			if (pubKey == null)
			{
				throw new ArgumentNullException(nameof(pubKey));
			}

			if (signature == null)
			{
				throw new ArgumentNullException(nameof(signature));
			}

			_knownSignatures.Add(Tuple.Create(pubKey, signature));
			return this;
		}

		public TransactionBuilder AddCoins(params ICoin[] coins)
		{
			return AddCoins((IEnumerable<ICoin>) coins);
		}

		public TransactionBuilder AddCoins(IEnumerable<ICoin> coins)
		{
			foreach (var coin in coins)
			{
				CurrentGroup._coins.AddOrReplace(coin.Outpoint, coin);
			}

			return this;
		}

		/// <summary>
		///     Set the name of this group (group are separated by call to Then())
		/// </summary>
		/// <param name="groupName">Name of the group</param>
		/// <returns></returns>
		public TransactionBuilder SetGroupName(string groupName)
		{
			CurrentGroup.Name = groupName;
			return this;
		}

		/// <summary>
		///     Send bitcoins to a destination
		/// </summary>
		/// <param name="destination">The destination</param>
		/// <param name="amount">The amount</param>
		/// <returns></returns>
		public TransactionBuilder Send(IDestination destination, Money amount)
		{
			return Send(destination.ScriptPubKey, amount);
		}

		/// <summary>
		///     Send bitcoins to a destination
		/// </summary>
		/// <param name="scriptPubKey">The destination</param>
		/// <param name="amount">The amount</param>
		/// <returns></returns>
		public TransactionBuilder Send(Script scriptPubKey, Money amount)
		{
			if (amount < Money.Zero)
			{
				throw new ArgumentOutOfRangeException("amount", "amount can't be negative");
			}

			_lastSendBuilder = null; //If the amount is dust, we don't want the fee to be paid by the previous Send
			if (DustPrevention && amount < GetDust(scriptPubKey) && !OpReturnTemplate.CheckScriptPubKey(scriptPubKey))
			{
				SendFees(amount);
				return this;
			}

			var builder = new SendBuilder(CreateTxOut(amount, scriptPubKey));
			CurrentGroup._builders.Add(builder.Build);
			_lastSendBuilder = builder;
			return this;
		}

		/// <summary>
		///     Will subtract fees from the previous TxOut added by the last TransactionBuidler.Send() call
		/// </summary>
		/// <returns></returns>
		public TransactionBuilder SubtractFees()
		{
			if (_lastSendBuilder == null)
			{
				throw new InvalidOperationException(
					"No call to TransactionBuilder.Send has been done which can support the fees");
			}

			_substractFeeBuilder = _lastSendBuilder;
			return this;
		}

		/// <summary>
		///     Send a money amount to the destination
		/// </summary>
		/// <param name="destination">The destination</param>
		/// <param name="amount">The amount (supported : Money, AssetMoney, MoneyBag)</param>
		/// <returns></returns>
		/// <exception cref="System.NotSupportedException">The coin type is not supported</exception>
		public TransactionBuilder Send(IDestination destination, IMoney amount)
		{
			return Send(destination.ScriptPubKey, amount);
		}

		/// <summary>
		///     Send a money amount to the destination
		/// </summary>
		/// <param name="destination">The destination</param>
		/// <param name="amount">The amount (supported : Money, AssetMoney, MoneyBag)</param>
		/// <returns></returns>
		/// <exception cref="System.NotSupportedException">The coin type is not supported</exception>
		public TransactionBuilder Send(Script scriptPubKey, IMoney amount)
		{
			var bag = amount as MoneyBag;
			if (bag != null)
			{
				foreach (var money in bag)
				{
					Send(scriptPubKey, amount);
				}

				return this;
			}

			var coinAmount = amount as Money;
			if (coinAmount != null)
			{
				return Send(scriptPubKey, coinAmount);
			}

			throw new NotSupportedException("Type of Money not supported");
		}

		[Obsolete("Transaction builder is automatically shuffled")]
		public TransactionBuilder Shuffle()
		{
			DoShuffle();
			return this;
		}

		private void DoShuffle()
		{
			if (ShuffleRandom != null)
			{
				Utils.Shuffle(_builderGroups, ShuffleRandom);
				foreach (var group in _builderGroups)
				{
					group.Shuffle();
				}
			}
		}

		internal Money GetDust()
		{
			return GetDust(new Script(new byte[25]));
		}

		internal Money GetDust(Script script)
		{
			if (StandardTransactionPolicy == null || StandardTransactionPolicy.MinRelayTxFee == null)
			{
				return Money.Zero;
			}

			return CreateTxOut(Money.Zero, script).GetDustThreshold(StandardTransactionPolicy.MinRelayTxFee);
		}

		/// <summary>
		///     Set transaction policy fluently
		/// </summary>
		/// <param name="policy">The policy</param>
		/// <returns>this</returns>
		public TransactionBuilder SetTransactionPolicy(StandardTransactionPolicy policy)
		{
			StandardTransactionPolicy = policy;
			return this;
		}

		private void AssertOpReturn(string name)
		{
			if (_opReturnUser == null)
			{
				_opReturnUser = name;
			}
			else
			{
				if (_opReturnUser != name)
				{
					throw new InvalidOperationException("Op return already used for " + _opReturnUser);
				}
			}
		}

		public TransactionBuilder SendFees(Money fees)
		{
			if (fees == null)
			{
				throw new ArgumentNullException(nameof(fees));
			}

			CurrentGroup._builders.Add(ctx => fees);
			_totalFee += fees;
			return this;
		}

		/// <summary>
		///     Split the estimated fees accross the several groups (separated by Then())
		/// </summary>
		/// <param name="feeRate"></param>
		/// <returns></returns>
		public TransactionBuilder SendEstimatedFees(FeeRate feeRate)
		{
			FilterUneconomicalCoinsRate = feeRate;
			var fee = EstimateFees(feeRate);
			SendFees(fee);
			return this;
		}

		/// <summary>
		///     Estimate the fee needed for the transaction, and split among groups according to their fee weight
		/// </summary>
		/// <param name="feeRate"></param>
		/// <returns></returns>
		public TransactionBuilder SendEstimatedFeesSplit(FeeRate feeRate)
		{
			FilterUneconomicalCoinsRate = feeRate;
			var fee = EstimateFees(feeRate);
			SendFeesSplit(fee);
			return this;
		}

		/// <summary>
		///     Send the fee splitted among groups according to their fee weight
		/// </summary>
		/// <param name="fees"></param>
		/// <returns></returns>
		public TransactionBuilder SendFeesSplit(Money fees)
		{
			if (fees == null)
			{
				throw new ArgumentNullException(nameof(fees));
			}

			var lastGroup = CurrentGroup; //Make sure at least one group exists
			var totalWeight = _builderGroups.Select(b => b.FeeWeight).Sum();
			var totalSent = Money.Zero;
			foreach (var group in _builderGroups)
			{
				var groupFee = Money.Satoshis(group.FeeWeight / totalWeight * fees.Satoshi);
				totalSent += groupFee;
				if (_builderGroups.Last() == group)
				{
					var leftOver = fees - totalSent;
					groupFee += leftOver;
				}

				group._builders.Add(ctx => groupFee);
			}

			return this;
		}


		/// <summary>
		///     If using SendFeesSplit or SendEstimatedFeesSplit, determine the weight this group participate in paying the fees
		/// </summary>
		/// <param name="feeWeight">The weight of fee participation</param>
		/// <returns></returns>
		public TransactionBuilder SetFeeWeight(decimal feeWeight)
		{
			CurrentGroup.FeeWeight = feeWeight;
			return this;
		}

		public TransactionBuilder SetChange(IDestination destination, ChangeType changeType = ChangeType.All)
		{
			return SetChange(destination.ScriptPubKey, changeType);
		}

		public TransactionBuilder SetChange(Script scriptPubKey, ChangeType changeType = ChangeType.All)
		{
			if ((changeType & ChangeType.Colored) != 0)
			{
				CurrentGroup._changeScript[(int) ChangeType.Colored] = scriptPubKey;
			}

			if ((changeType & ChangeType.Uncolored) != 0)
			{
				CurrentGroup._changeScript[(int) ChangeType.Uncolored] = scriptPubKey;
			}

			return this;
		}

		[Obsolete(
			"Use ConsensusFactory.CreateTransactionBuilder() instead, so you don't have to use this method anymore")]
		public TransactionBuilder SetConsensusFactory(ConsensusFactory consensusFactory)
		{
			ConsensusFactory = consensusFactory ?? Network.Main.Consensus.ConsensusFactory;
			return this;
		}

		[Obsolete("Use Network.CreateTransactionBuilder() instead, so you don't have to use this method anymore")]
		public TransactionBuilder SetConsensusFactory(Network network)
		{
			return SetConsensusFactory(network?.Consensus?.ConsensusFactory);
		}

		public TransactionBuilder SetCoinSelector(ICoinSelector selector)
		{
			if (selector == null)
			{
				throw new ArgumentNullException(nameof(selector));
			}

			CoinSelector = selector;
			return this;
		}

		/// <summary>
		///     Build the transaction
		/// </summary>
		/// <param name="sign">True if signs all inputs with the available keys</param>
		/// <returns>The transaction</returns>
		/// <exception cref="BitcoinNet.NotEnoughFundsException">Not enough funds are available</exception>
		public Transaction BuildTransaction(bool sign)
		{
			var tx = BuildTransaction(sign, SigHash.All);
			_built = true;
			return tx;
		}

		/// <summary>
		///     Build the transaction
		/// </summary>
		/// <param name="sign">True if signs all inputs with the available keys</param>
		/// <param name="sigHash">The type of signature</param>
		/// <returns>The transaction</returns>
		/// <exception cref="BitcoinNet.NotEnoughFundsException">Not enough funds are available</exception>
		public Transaction BuildTransaction(bool sign, SigHash sigHash)
		{
			DoShuffle();
			var ctx = new TransactionBuildingContext(this);
			if (_completedTransaction != null)
			{
				ctx.Transaction = _completedTransaction.Clone();
			}

			if (_lockTime != null)
			{
				ctx.Transaction.LockTime = _lockTime.Value;
			}

			foreach (var group in _builderGroups)
			{
				ctx.Group = group;
				ctx.AdditionalBuilders.Clear();
				ctx.AdditionalFees = Money.Zero;

				// TODO (Osman): Revisit following lines which are remainings of OpenAsset
				ctx.ChangeType = ChangeType.Colored;

				ctx.AdditionalBuilders.Add(_ => _.AdditionalFees);
				ctx.Dust = GetDust();
				ctx.ChangeAmount = Money.Zero;
				ctx.CoverOnly = group.CoverOnly;
				ctx.ChangeType = ChangeType.Uncolored;
				BuildTransaction(ctx, group, group._builders, group._coins.Values.OfType<Coin>().Where(IsEconomical),
					Money.Zero);
			}

			if (sign)
			{
				SignTransactionInPlace(ctx.Transaction, sigHash);
			}

			return ctx.Transaction;
		}

		private bool IsEconomical(Coin c)
		{
			if (!FilterUneconomicalCoins || FilterUneconomicalCoinsRate == null)
			{
				return true;
			}

			var baseSize = 0;
			EstimateScriptSigSize(c, ref baseSize);
			var vSize = baseSize;
			return c.Amount >= FilterUneconomicalCoinsRate.GetFee(vSize);
		}

		private IEnumerable<ICoin> BuildTransaction(
			TransactionBuildingContext ctx,
			BuilderGroup group,
			IEnumerable<Builder> builders,
			IEnumerable<ICoin> coins,
			IMoney zero)
		{
			var originalCtx = ctx.CreateMemento();
			var fees = _totalFee + ctx.AdditionalFees;

			// Replace the _SubstractFeeBuilder by another one with the fees substracts
			var builderList = builders.ToList();
			for (var i = 0; i < builderList.Count; i++)
			{
				if (builderList[i].Target == _substractFeeBuilder)
				{
					builderList.Remove(builderList[i]);
					var newTxOut = _substractFeeBuilder._txOut.Clone();
					var minimumTxOutValue = DustPrevention ? GetDust(newTxOut.ScriptPubKey) : Money.Zero;
					newTxOut.Value -= fees;
					if (newTxOut.Value < Money.Zero)
					{
						throw new NotEnoughFundsException(
							"Can't substract fee from this output because the amount is too small",
							group.Name,
							-newTxOut.Value
						);
					}

					if (newTxOut.Value >= minimumTxOutValue)
					{
						builderList.Insert(i, new SendBuilder(newTxOut).Build);
					}
					else
					{
						fees += newTxOut.Value;
						builderList.Insert(i, _ => newTxOut.Value);
					}
				}
			}
			////////////////////////////////////////////////////////

			var target = builderList.Concat(ctx.AdditionalBuilders).Select(b => b(ctx)).Sum(zero);
			if (ctx.CoverOnly != null)
			{
				target = ctx.CoverOnly.Add(ctx.ChangeAmount);
			}

			var unconsumed = coins.Where(c => ctx.ConsumedCoins.All(cc => cc.Outpoint != c.Outpoint));
			var selection = CoinSelector.Select(unconsumed, target);
			if (selection == null)
			{
				throw new NotEnoughFundsException("Not enough funds to cover the target",
					group.Name,
					target.Sub(unconsumed.Select(u => u.Amount).Sum(zero))
				);
			}

			var total = selection.Select(s => s.Amount).Sum(zero);
			var change = total.Sub(target);
			if (change.CompareTo(zero) == -1)
			{
				throw new NotEnoughFundsException("Not enough funds to cover the target",
					group.Name,
					change.Negate()
				);
			}

			if (change.CompareTo(ctx.Dust) == 1)
			{
				var changeScript = group._changeScript[(int) ctx.ChangeType];
				if (changeScript == null)
				{
					throw new InvalidOperationException("A change address should be specified (" + ctx.ChangeType +
					                                    ")");
				}

				if (!(ctx.Dust is Money) || change.CompareTo(GetDust(changeScript)) == 1)
				{
					ctx.RestoreMemento(originalCtx);
					ctx.ChangeAmount = change;
					try
					{
						return BuildTransaction(ctx, group, builders, coins, zero);
					}
					finally
					{
						ctx.ChangeAmount = zero;
					}
				}
			}

			foreach (var coin in selection)
			{
				ctx.ConsumedCoins.Add(coin);
				var input = ctx.Transaction.Inputs.FirstOrDefault(i => i.PrevOut == coin.Outpoint);
				if (input == null)
				{
					input = ctx.Transaction.Inputs.Add(coin.Outpoint);
				}

				if (_lockTime != null && !ctx.NonFinalSequenceSet)
				{
					input.Sequence = 0;
					ctx.NonFinalSequenceSet = true;
				}
			}

			if (MergeOutputs)
			{
				var collapsedOutputs = ctx.Transaction.Outputs
					.GroupBy(o => o.ScriptPubKey)
					.Select(o =>
						o.Count() == 1
							? o.First()
							: ctx.Transaction.Outputs.CreateNewTxOut(o.Select(txout => txout.Value).Sum(), o.Key))
					.ToArray();
				if (collapsedOutputs.Length < ctx.Transaction.Outputs.Count)
				{
					ctx.Transaction.Outputs.Clear();
					ctx.Transaction.Outputs.AddRange(collapsedOutputs);
				}
			}

			return selection;
		}

		public Transaction SignTransaction(Transaction transaction, SigHash sigHash)
		{
			var tx = transaction.Clone();
			SignTransactionInPlace(tx, sigHash);
			return tx;
		}

		public Transaction SignTransaction(Transaction transaction)
		{
			return SignTransaction(transaction, SigHash.All);
		}

		public Transaction SignTransactionInPlace(Transaction transaction)
		{
			return SignTransactionInPlace(transaction, SigHash.All);
		}

		public Transaction SignTransactionInPlace(Transaction transaction, SigHash sigHash)
		{
			var ctx = new TransactionSigningContext(this, transaction);
			if (transaction is IHasForkId hasForkId)
			{
				sigHash = (SigHash) ((uint) sigHash | 0x40u);
			}

			ctx.SigHash = sigHash;
			foreach (var input in transaction.Inputs.AsIndexedInputs())
			{
				var coin = FindSignableCoin(input);
				if (coin != null)
				{
					Sign(ctx, coin, input);
				}
			}

			return transaction;
		}

		public ICoin FindSignableCoin(IndexedTxIn txIn)
		{
			var coin = FindCoin(txIn.PrevOut);
			if (coin == null || coin is ScriptCoin)
			{
				return coin;
			}

			var hash = ScriptCoin.GetRedeemHash(coin.TxOut.ScriptPubKey);
			if (hash != null)
			{
				var redeem = _scriptPubKeyToRedeem.TryGet(coin.TxOut.ScriptPubKey);
				if (redeem != null && PayToScriptHashTemplate.Instance.CheckScriptPubKey(redeem))
				{
					redeem = _scriptPubKeyToRedeem.TryGet(redeem);
				}

				if (redeem == null)
				{
					if (hash is ScriptId)
					{
						var parameters =
							PayToScriptHashTemplate.Instance.ExtractScriptSigParameters(txIn.ScriptSig,
								(ScriptId) hash);
						if (parameters != null)
						{
							redeem = parameters.RedeemScript;
						}
					}
				}

				if (redeem != null)
				{
					return new ScriptCoin(coin, redeem);
				}
			}

			return coin;
		}

		/// <summary>
		///     Verify that a transaction is fully signed and have enough fees
		/// </summary>
		/// <param name="tx">The transaction to check</param>
		/// <returns>True if no error</returns>
		public bool Verify(Transaction tx)
		{
			return Verify(tx, null as Money, out _);
		}

		/// <summary>
		///     Verify that a transaction is fully signed and have enough fees
		/// </summary>
		/// <param name="tx">The transaction to check</param>
		/// <param name="expectedFees">The expected fees (more or less 10%)</param>
		/// <returns>True if no error</returns>
		public bool Verify(Transaction tx, Money expectedFees)
		{
			return Verify(tx, expectedFees, out _);
		}

		/// <summary>
		///     Verify that a transaction is fully signed and have enough fees
		/// </summary>
		/// <param name="tx">The transaction to check</param>
		/// <param name="expectedFeeRate">The expected fee rate</param>
		/// <returns>True if no error</returns>
		public bool Verify(Transaction tx, FeeRate expectedFeeRate)
		{
			return Verify(tx, expectedFeeRate, out _);
		}

		/// <summary>
		///     Verify that a transaction is fully signed and have enough fees
		/// </summary>
		/// <param name="tx">The transaction to check</param>
		/// <param name="errors">Detected errors</param>
		/// <returns>True if no error</returns>
		public bool Verify(Transaction tx, out TransactionPolicyError[] errors)
		{
			return Verify(tx, null as Money, out errors);
		}

		/// <summary>
		///     Verify that a transaction is fully signed, have enough fees, and follow the Standard and Miner Transaction Policy
		///     rules
		/// </summary>
		/// <param name="tx">The transaction to check</param>
		/// <param name="expectedFees">The expected fees (more or less 10%)</param>
		/// <param name="errors">Detected errors</param>
		/// <returns>True if no error</returns>
		public bool Verify(Transaction tx, Money expectedFees, out TransactionPolicyError[] errors)
		{
			if (tx == null)
			{
				throw new ArgumentNullException(nameof(tx));
			}

			var coins = tx.Inputs.Select(i => FindCoin(i.PrevOut)).Where(c => c != null).ToArray();
			var exceptions = new List<TransactionPolicyError>();
			var policyErrors = MinerTransactionPolicy.Instance.Check(tx, coins);
			exceptions.AddRange(policyErrors);
			policyErrors = StandardTransactionPolicy.Check(tx, coins);
			exceptions.AddRange(policyErrors);
			if (expectedFees != null)
			{
				var fees = tx.GetFee(coins);
				if (fees != null)
				{
					var margin = Money.Zero;
					if (DustPrevention)
					{
						margin = GetDust() * 2;
					}

					if (!fees.Almost(expectedFees, margin))
					{
						exceptions.Add(new NotEnoughFundsPolicyError("Fees different than expected",
							expectedFees - fees));
					}
				}
			}

			errors = exceptions.ToArray();
			return errors.Length == 0;
		}

		/// <summary>
		///     Verify that a transaction is fully signed and have enough fees
		/// </summary>
		/// <param name="tx">The transaction to check</param>
		/// <param name="expectedFeeRate">The expected fee rate</param>
		/// <param name="errors">Detected errors</param>
		/// <returns>True if no error</returns>
		public bool Verify(Transaction tx, FeeRate expectedFeeRate, out TransactionPolicyError[] errors)
		{
			if (tx == null)
			{
				throw new ArgumentNullException(nameof(tx));
			}

			return Verify(tx, expectedFeeRate == null ? null : expectedFeeRate.GetFee(tx), out errors);
		}

		/// <summary>
		///     Verify that a transaction is fully signed and have enough fees
		/// </summary>
		/// <param name="tx">he transaction to check</param>
		/// <param name="expectedFeeRate">The expected fee rate</param>
		/// <returns>Detected errors</returns>
		public TransactionPolicyError[] Check(Transaction tx, FeeRate expectedFeeRate)
		{
			return Check(tx, expectedFeeRate == null ? null : expectedFeeRate.GetFee(tx));
		}

		/// <summary>
		///     Verify that a transaction is fully signed and have enough fees
		/// </summary>
		/// <param name="tx">he transaction to check</param>
		/// <param name="expectedFee">The expected fee</param>
		/// <returns>Detected errors</returns>
		public TransactionPolicyError[] Check(Transaction tx, Money expectedFee)
		{
			Verify(tx, expectedFee, out var errors);
			return errors;
		}

		/// <summary>
		///     Verify that a transaction is fully signed and have enough fees
		/// </summary>
		/// <param name="tx">he transaction to check</param>
		/// <returns>Detected errors</returns>
		public TransactionPolicyError[] Check(Transaction tx)
		{
			return Check(tx, null as Money);
		}

		private CoinNotFoundException CoinNotFound(IndexedTxIn txIn)
		{
			return new CoinNotFoundException(txIn);
		}


		public ICoin FindCoin(OutPoint outPoint)
		{
			var result = _builderGroups.Select(c => c._coins.TryGet(outPoint)).FirstOrDefault(r => r != null);
			if (result == null && CoinFinder != null)
			{
				result = CoinFinder(outPoint);
			}

			return result;
		}

		/// <summary>
		///     Find spent coins of a transaction
		/// </summary>
		/// <param name="tx">The transaction</param>
		/// <returns>Array of size tx.Input.Count, if a coin is not fund, a null coin is returned.</returns>
		public ICoin[] FindSpentCoins(Transaction tx)
		{
			return
				tx
					.Inputs
					.Select(i => FindCoin(i.PrevOut))
					.ToArray();
		}

		/// <summary>
		///     Estimate the physical size of the transaction
		/// </summary>
		/// <param name="tx">The transaction to be estimated</param>
		/// <returns></returns>
		public int EstimateSize(Transaction tx)
		{
			if (tx == null)
			{
				throw new ArgumentNullException(nameof(tx));
			}

			var clone = tx.Clone();
			clone.Inputs.Clear();
			var baseSize = clone.GetSerializedSize() - 1;
			baseSize += new VarInt((ulong) tx.Inputs.Count).GetSerializedSize();

			foreach (var txin in tx.Inputs.AsIndexedInputs())
			{
				var coin = FindSignableCoin(txin) ?? FindCoin(txin.PrevOut);
				if (coin == null)
				{
					throw CoinNotFound(txin);
				}

				EstimateScriptSigSize(coin, ref baseSize);
				baseSize += 32 + 4 + 4;
			}

			return baseSize;
		}

		private TxOut CreateTxOut(Money amount = null, Script script = null)
		{
			if (!ConsensusFactory.TryCreateNew<TxOut>(out var txOut))
			{
				txOut = new TxOut();
			}

			if (amount != null)
			{
				txOut.Value = amount;
			}

			if (script != null)
			{
				txOut.ScriptPubKey = script;
			}

			return txOut;
		}

		private void EstimateScriptSigSize(ICoin coin, ref int baseSize)
		{
			var p2shPushRedeemSize = 0;
			if (coin is ScriptCoin)
			{
				var scriptCoin = (ScriptCoin) coin;
				var p2sh = scriptCoin.GetP2SHRedeem();
				if (p2sh != null)
				{
					coin = new Coin(scriptCoin.Outpoint, CreateTxOut(scriptCoin.Amount, p2sh));
					p2shPushRedeemSize = new Script(Op.GetPushOp(p2sh.ToBytes(true))).Length;
					baseSize += p2shPushRedeemSize;
				}
			}

			var scriptPubkey = coin.GetScriptCode();
			var scriptSigSize = -1;
			foreach (var extension in Extensions)
			{
				if (extension.CanEstimateScriptSigSize(scriptPubkey))
				{
					scriptSigSize = extension.EstimateScriptSigSize(scriptPubkey);
					break;
				}
			}

			if (scriptSigSize == -1)
			{
				scriptSigSize +=
					coin.TxOut.ScriptPubKey.Length; //Using heurestic to approximate size of unknown scriptPubKey
			}

			baseSize += scriptSigSize + new VarInt((ulong) (scriptSigSize + p2shPushRedeemSize)).GetSerializedSize();
		}

		/// <summary>
		///     Estimate fees of the built transaction
		/// </summary>
		/// <param name="feeRate">Fee rate</param>
		/// <returns></returns>
		public Money EstimateFees(FeeRate feeRate)
		{
			if (feeRate == null)
			{
				throw new ArgumentNullException(nameof(feeRate));
			}

			var feeBuilders = new List<Builder>();
			var feeSent = Money.Zero;
			try
			{
				while (true)
				{
					var tx = BuildTransaction(false);
					var shouldSend = EstimateFees(tx, feeRate);
					var delta = shouldSend - feeSent;
					if (delta <= Money.Zero)
					{
						break;
					}

					SendFees(delta);
					feeBuilders.Add(CurrentGroup._builders[CurrentGroup._builders.Count - 1]);
					feeSent += delta;
				}
			}
			finally
			{
				foreach (var feeBuilder in feeBuilders)
				{
					CurrentGroup._builders.Remove(feeBuilder);
				}

				_totalFee -= feeSent;
			}

			return feeSent;
		}

		/// <summary>
		///     Estimate fees of an unsigned transaction
		/// </summary>
		/// <param name="tx"></param>
		/// <param name="feeRate">Fee rate</param>
		/// <returns></returns>
		public Money EstimateFees(Transaction tx, FeeRate feeRate)
		{
			if (tx == null)
			{
				throw new ArgumentNullException(nameof(tx));
			}

			if (feeRate == null)
			{
				throw new ArgumentNullException(nameof(feeRate));
			}

			var estimation = EstimateSize(tx);
			return feeRate.GetFee(estimation);
		}

		private void Sign(TransactionSigningContext ctx, ICoin coin, IndexedTxIn txIn)
		{
			var input = txIn.TxIn;
			var scriptSig = CreateScriptSig(ctx, coin, txIn);
			if (scriptSig == null)
			{
				return;
			}

			var scriptCoin = coin as ScriptCoin;

			var signatures = txIn.ScriptSig;
			if (scriptCoin != null && scriptCoin.RedeemType == RedeemType.P2SH)
			{
				signatures = RemoveRedeem(signatures);
			}

			signatures = CombineScriptSigs(coin, scriptSig, signatures);

			txIn.ScriptSig = signatures;
			if (scriptCoin != null && scriptCoin.RedeemType == RedeemType.P2SH)
			{
				txIn.ScriptSig = input.ScriptSig + Op.GetPushOp(scriptCoin.GetP2SHRedeem().ToBytes(true));
			}
		}


		private static Script RemoveRedeem(Script script)
		{
			if (script == Script.Empty)
			{
				return script;
			}

			var ops = script.ToOps().ToArray();
			return new Script(ops.Take(ops.Length - 1));
		}

		private Script CombineScriptSigs(ICoin coin, Script a, Script b)
		{
			var scriptPubkey = coin.GetScriptCode();
			if (Script.IsNullOrEmpty(a))
			{
				return b ?? Script.Empty;
			}

			if (Script.IsNullOrEmpty(b))
			{
				return a ?? Script.Empty;
			}

			foreach (var extension in Extensions)
			{
				if (extension.CanCombineScriptSig(scriptPubkey, a, b))
				{
					return extension.CombineScriptSig(scriptPubkey, a, b);
				}
			}

			return a.Length > b.Length ? a : b; //Heurestic
		}

		private Script CreateScriptSig(TransactionSigningContext ctx, ICoin coin, IndexedTxIn txIn)
		{
			var scriptPubKey = coin.GetScriptCode();
			var keyRepo = new TransactionBuilderKeyRepository(this, ctx);
			var signer = new TransactionBuilderSigner(coin, ctx.SigHash, txIn);

			var signer2 = new KnownSignatureSigner(_knownSignatures, coin, ctx.SigHash, txIn);

			foreach (var extension in Extensions)
			{
				if (extension.CanGenerateScriptSig(scriptPubKey))
				{
					var scriptSig1 = extension.GenerateScriptSig(scriptPubKey, keyRepo, signer);
					var scriptSig2 = extension.GenerateScriptSig(scriptPubKey, signer2, signer2);
					if (scriptSig2 != null)
					{
						scriptSig2 = signer2.ReplaceDummyKeys(scriptSig2);
					}

					if (scriptSig1 != null && scriptSig2 != null &&
					    extension.CanCombineScriptSig(scriptPubKey, scriptSig1, scriptSig2))
					{
						var combined = extension.CombineScriptSig(scriptPubKey, scriptSig1, scriptSig2);
						return combined;
					}

					return scriptSig1 ?? scriptSig2;
				}
			}

			throw new NotSupportedException("Unsupported scriptPubKey");
		}

		private Key FindKey(TransactionSigningContext ctx, Script scriptPubKey)
		{
			var key = _keys
				.Concat(ctx.AdditionalKeys)
				.FirstOrDefault(k => IsCompatibleKey(k.PubKey, scriptPubKey));
			if (key == null && KeyFinder != null)
			{
				key = KeyFinder(scriptPubKey);
			}

			return key;
		}

		private static bool IsCompatibleKey(PubKey k, Script scriptPubKey)
		{
			return k.ScriptPubKey == scriptPubKey || //P2PK
			       k.Hash.ScriptPubKey == scriptPubKey || //P2PKH
			       k.ScriptPubKey.Hash.ScriptPubKey == scriptPubKey || //P2PK P2SH
			       k.Hash.ScriptPubKey.Hash.ScriptPubKey == scriptPubKey; //P2PKH P2SH
		}

		/// <summary>
		///     Create a new participant in the transaction with its own set of coins and keys
		/// </summary>
		/// <returns></returns>
		public TransactionBuilder Then()
		{
			_currentGroup = null;
			return this;
		}

		/// <summary>
		///     Switch to another participant in the transaction, or create a new one if it is not found.
		/// </summary>
		/// <returns></returns>
		public TransactionBuilder Then(string groupName)
		{
			var group = _builderGroups.FirstOrDefault(g => g.Name == groupName);
			if (group == null)
			{
				group = new BuilderGroup(this);
				_builderGroups.Add(group);
				group.Name = groupName;
			}

			_currentGroup = group;
			return this;
		}

		/// <summary>
		///     Specify the amount of money to cover txouts, if not specified all txout will be covered
		/// </summary>
		/// <param name="amount"></param>
		/// <returns></returns>
		public TransactionBuilder CoverOnly(Money amount)
		{
			CurrentGroup.CoverOnly = amount;
			return this;
		}

		/// <summary>
		///     Allows to keep building on the top of a partially built transaction
		/// </summary>
		/// <param name="transaction">Transaction to complete</param>
		/// <returns></returns>
		public TransactionBuilder ContinueToBuild(Transaction transaction)
		{
			if (_built)
			{
				throw new InvalidOperationException(
					"ContinueToBuild must be called with a new TransactionBuilder instance");
			}

			if (_completedTransaction != null)
			{
				throw new InvalidOperationException("Transaction to complete already set");
			}

			_completedTransaction = transaction.Clone();
			return this;
		}

		/// <summary>
		///     Will cover the remaining amount of TxOut of a partially built transaction (to call after ContinueToBuild)
		/// </summary>
		/// <returns></returns>
		public TransactionBuilder CoverTheRest()
		{
			if (_completedTransaction == null)
			{
				throw new InvalidOperationException(
					"A partially built transaction should be specified by calling ContinueToBuild");
			}

			var spent = _completedTransaction.Inputs.AsIndexedInputs().Select(txin =>
				{
					var c = FindCoin(txin.PrevOut);
					if (c == null)
					{
						throw CoinNotFound(txin);
					}

					if (!(c is Coin))
					{
						return null;
					}

					return (Coin) c;
				})
				.Where(c => c != null)
				.Select(c => c.Amount)
				.Sum();

			var toComplete = _completedTransaction.TotalOut - spent;
			CurrentGroup._builders.Add(ctx =>
			{
				if (toComplete < Money.Zero)
				{
					return Money.Zero;
				}

				return toComplete;
			});
			return this;
		}

		public TransactionBuilder AddCoins(Transaction transaction)
		{
			var txId = transaction.GetHash();
			AddCoins(transaction.Outputs.Select((o, i) => new Coin(txId, (uint) i, o.Value, o.ScriptPubKey)).ToArray());
			return this;
		}

		public TransactionBuilder AddKnownRedeems(params Script[] knownRedeems)
		{
			foreach (var redeem in knownRedeems)
			{
				_scriptPubKeyToRedeem.AddOrReplace(redeem.Hash.ScriptPubKey, redeem); //Might be P2SH
			}

			return this;
		}

		public Transaction CombineSignatures(params Transaction[] transactions)
		{
			if (transactions.Length == 1)
			{
				return transactions[0];
			}

			if (transactions.Length == 0)
			{
				return null;
			}

			var tx = transactions[0].Clone();
			for (var i = 1; i < transactions.Length; i++)
			{
				var signed = transactions[i];
				tx = CombineSignaturesCore(tx, signed);
			}

			return tx;
		}

		private Transaction CombineSignaturesCore(Transaction signed1, Transaction signed2)
		{
			if (signed1 == null)
			{
				return signed2;
			}

			if (signed2 == null)
			{
				return signed1;
			}

			var tx = signed1.Clone();
			for (var i = 0; i < tx.Inputs.Count; i++)
			{
				if (i >= signed2.Inputs.Count)
				{
					break;
				}

				var txIn = tx.Inputs[i];

				var coin = FindCoin(txIn.PrevOut);
				var scriptPubKey = coin == null
					? DeduceScriptPubKey(txIn.ScriptSig) ?? DeduceScriptPubKey(signed2.Inputs[i].ScriptSig)
					: coin.TxOut.ScriptPubKey;

				Money amount = null;
				if (coin != null)
				{
					amount = ((Coin) coin).Amount;
				}

				var result = Script.CombineSignatures(
					scriptPubKey,
					new TransactionChecker(tx, i, amount),
					GetScriptSigs(signed1.Inputs.AsIndexedInputs().Skip(i).First()),
					GetScriptSigs(signed2.Inputs.AsIndexedInputs().Skip(i).First()));
				var input = tx.Inputs.AsIndexedInputs().Skip(i).First();
				input.ScriptSig = result.ScriptSig;
			}

			return tx;
		}

		private ScriptSigs GetScriptSigs(IndexedTxIn indexedTxIn)
		{
			return new ScriptSigs
			{
				ScriptSig = indexedTxIn.ScriptSig
			};
		}

		private Script DeduceScriptPubKey(Script scriptSig)
		{
			var p2sh = PayToScriptHashTemplate.Instance.ExtractScriptSigParameters(scriptSig);
			if (p2sh != null && p2sh.RedeemScript != null)
			{
				return p2sh.RedeemScript.Hash.ScriptPubKey;
			}

			foreach (var extension in Extensions)
			{
				if (extension.CanDeduceScriptPubKey(scriptSig))
				{
					return extension.DeduceScriptPubKey(scriptSig);
				}
			}

			return null;
		}

		internal class TransactionBuilderSigner : ISigner
		{
			private readonly ICoin _coin;
			private readonly SigHash _sigHash;
			private readonly IndexedTxIn _txIn;

			public TransactionBuilderSigner(ICoin coin, SigHash sigHash, IndexedTxIn txIn)
			{
				_coin = coin;
				_sigHash = sigHash;
				_txIn = txIn;
			}

			// ISigner Members

			public TransactionSignature Sign(Key key)
			{
				return _txIn.Sign(key, _coin, _sigHash);
			}
		}

		internal class TransactionBuilderKeyRepository : IKeyRepository
		{
			private readonly TransactionSigningContext _ctx;
			private readonly TransactionBuilder _txBuilder;

			public TransactionBuilderKeyRepository(TransactionBuilder txBuilder, TransactionSigningContext ctx)
			{
				_ctx = ctx;
				_txBuilder = txBuilder;
			}

			// IKeyRepository Members

			public Key FindKey(Script scriptPubkey)
			{
				return _txBuilder.FindKey(_ctx, scriptPubkey);
			}
		}

		private class KnownSignatureSigner : ISigner, IKeyRepository
		{
			private readonly ICoin _coin;
			private readonly Dictionary<uint256, PubKey> _dummyToRealKey = new Dictionary<uint256, PubKey>();
			private readonly List<Tuple<PubKey, ECDSASignature>> _knownSignatures;
			private readonly SigHash _sigHash;
			private readonly IndexedTxIn _txIn;

			private readonly Dictionary<KeyId, ECDSASignature> _verifiedSignatures =
				new Dictionary<KeyId, ECDSASignature>();

			public KnownSignatureSigner(List<Tuple<PubKey, ECDSASignature>> _KnownSignatures, ICoin coin,
				SigHash sigHash, IndexedTxIn txIn)
			{
				_knownSignatures = _KnownSignatures;
				_coin = coin;
				_sigHash = sigHash;
				_txIn = txIn;
			}

			public Key FindKey(Script scriptPubKey)
			{
				foreach (var tv in _knownSignatures.Where(tv => IsCompatibleKey(tv.Item1, scriptPubKey)))
				{
					var hash = _txIn.GetSignatureHash(_coin, _sigHash);
					if (tv.Item1.Verify(hash, tv.Item2))
					{
						var key = new Key();
						_dummyToRealKey.Add(Hashes.Hash256(key.PubKey.ToBytes()), tv.Item1);
						_verifiedSignatures.AddOrReplace(key.PubKey.Hash, tv.Item2);
						return key;
					}
				}

				return null;
			}

			public TransactionSignature Sign(Key key)
			{
				return new TransactionSignature(_verifiedSignatures[key.PubKey.Hash], _sigHash);
			}

			public Script ReplaceDummyKeys(Script script)
			{
				var ops = script.ToOps().ToList();
				var result = new List<Op>();
				foreach (var op in ops)
				{
					var h = Hashes.Hash256(op.PushData);
					if (_dummyToRealKey.TryGetValue(h, out var real))
					{
						result.Add(Op.GetPushOp(real.ToBytes()));
					}
					else
					{
						result.Add(op);
					}
				}

				return new Script(result.ToArray());
			}
		}

		internal class TransactionSigningContext
		{
			public TransactionSigningContext(TransactionBuilder builder, Transaction transaction)
			{
				Builder = builder;
				Transaction = transaction;
			}

			public Transaction Transaction { get; set; }

			public TransactionBuilder Builder { get; set; }

			public List<Key> AdditionalKeys { get; } = new List<Key>();

			public SigHash SigHash { get; set; }
		}

		internal class TransactionBuildingContext
		{
			public TransactionBuildingContext(TransactionBuilder builder)
			{
				Builder = builder;
				Transaction = builder.ConsensusFactory.CreateTransaction();
				AdditionalFees = Money.Zero;
			}

			public BuilderGroup Group { get; set; }

			public List<ICoin> ConsumedCoins { get; } = new List<ICoin>();

			public TransactionBuilder Builder { get; set; }

			public Transaction Transaction { get; set; }

			public Money AdditionalFees { get; set; }

			public List<Builder> AdditionalBuilders { get; } = new List<Builder>();

			public IMoney ChangeAmount { get; set; }

			public bool NonFinalSequenceSet { get; set; }

			public IMoney CoverOnly { get; set; }

			public IMoney Dust { get; set; }

			public ChangeType ChangeType { get; set; }

			public TransactionBuildingContext CreateMemento()
			{
				var memento = new TransactionBuildingContext(Builder);
				memento.RestoreMemento(this);
				return memento;
			}

			public void RestoreMemento(TransactionBuildingContext memento)
			{
				Transaction = memento.Transaction.Clone();
				AdditionalFees = memento.AdditionalFees;
			}
		}

		internal class BuilderGroup
		{
			internal readonly List<Builder> _builders = new List<Builder>();
			internal readonly Script[] _changeScript = new Script[3];
			internal readonly Dictionary<OutPoint, ICoin> _coins = new Dictionary<OutPoint, ICoin>();
			private readonly TransactionBuilder _parent;

			public BuilderGroup(TransactionBuilder parent)
			{
				_parent = parent;
				FeeWeight = 1.0m;
				_builders.Add(SetChange);
			}

			public Money CoverOnly { get; set; }

			public string Name { get; set; }

			public decimal FeeWeight { get; set; }

			private IMoney SetChange(TransactionBuildingContext ctx)
			{
				var changeAmount = (Money) ctx.ChangeAmount;
				if (changeAmount.Satoshi == 0)
				{
					return Money.Zero;
				}

				ctx.Transaction.Outputs.Add(changeAmount, ctx.Group._changeScript[(int) ChangeType.Uncolored]);
				return changeAmount;
			}

			internal void Shuffle()
			{
				Shuffle(_builders);
			}

			private void Shuffle(List<Builder> builders)
			{
				Utils.Shuffle(builders, _parent.ShuffleRandom);
			}
		}

		private class SendBuilder
		{
			internal readonly TxOut _txOut;

			public SendBuilder(TxOut txout)
			{
				_txOut = txout;
			}

			public Money Build(TransactionBuildingContext ctx)
			{
				ctx.Transaction.Outputs.Add(_txOut);
				return _txOut.Value;
			}
		}
	}

	public class CoinNotFoundException : KeyNotFoundException
	{
		public CoinNotFoundException(IndexedTxIn txIn)
			: base("No coin matching " + txIn.PrevOut + " was found")
		{
			OutPoint = txIn.PrevOut;
			InputIndex = txIn.Index;
		}

		public OutPoint OutPoint { get; }

		public uint InputIndex { get; }
	}
}