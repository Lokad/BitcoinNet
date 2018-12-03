# BitcoinNet

BitcoinNet is intended as a Bitcoin Cash library for the .NET platform.

The focus of BitcoinNet is provide robust, well-engineered, building blocks to interact with the BCH blockchain, either in batch, deserializing blocks and their transactions, or interactively, listening to the network and propagating transactions.

At this point, BitcoinNet does not intend to support any coin beyond Bitcoin Cash (BCH).

## Usage

BitcoinNet codebase is divided into 3 projects:

- `BitcoinNet`: Core functionality.
- `BitcoinNet.JsonRpc`: JSON-RPC logic for communicating with BCH nodes. The protocol documentation can be found at [official Bitcoin Cash repository](https://github.com/bitcoincashorg/bitcoincash.org/blob/master/spec/JSON-RPC.md).
- `BitcoinNet.Mnemonic`: Implementation of mnemonic code for generating deterministic keys ([BIP39](https://github.com/bitcoin/bips/blob/master/bip-0039.mediawiki)). 

## License

BitcoinNet is licensed under the [MIT License](https://opensource.org/licenses/MIT).