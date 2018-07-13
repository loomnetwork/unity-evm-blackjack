
# Solidity BlackJack + Unity Client

An example game of Blackjack that uses an EVM contract written in Solidity as backend, and Unity as a client, utilizing the [Loom Unity SDK](https://github.com/loomnetwork/unity3d-sdk).

Specifically, the game is [European No Hole Card Blackjack](https://www.topcasinos.com/casino-articles/european-no-hole-card-blackjack.html) with some simplifications. There is no doubling down, splitting pairs, and insurance.

# Development

## Running the DAppChain

```
# Download the project
git clone https://github.com/loomnetwork/unity-evm-blackjack.git
cd unity-evm-blackjack

# Compile and migrate the Truffle project. This will copy the ABI file to the Unity client,
# and compiled contract to the Loom DAppChain

cd truffle
truffle build
cd ..

# Run the Loom DAppChain. Loom binary will be downloaded automatically
cd dappchain
./start-chain.sh
```

## Running the Unity client
Open the Unity project located in `unityclient`. Open the `BlackJack/Game` scene and run/build it.

Loom Network
----
[https://loomx.io](https://loomx.io)


License
----

MIT

Third-party notice
----
Some art assets are provided by [icons8](https://icons8.com) under [CC BY-ND 3.0](https://creativecommons.org/licenses/by-nd/3.0/).