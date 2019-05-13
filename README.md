
# Deprecated Repository

This repository is **deprecated and no longer maintained**. Head over to the [Truffle DappChain Example](https://github.com/loomnetwork/truffle-dappchain-example) repository to learn how to build a simple web UI that interacts with Loom PlasmaChain.
Also, make sure to check this [video tutorial](https://www.youtube.com/watch?v=c04C95OEi-o&t=387s) that shows how you can create a super fast and gasless ERC20 payment system using Loom Plasmachain.


# Solidity BlackJack + Unity Client

An example game of Blackjack that uses an EVM contract written in Solidity as backend, and Unity as a client, utilizing the [Loom Unity SDK](https://github.com/loomnetwork/unity3d-sdk).

Specifically, the game is [European No Hole Card Blackjack](https://www.topcasinos.com/casino-articles/european-no-hole-card-blackjack.html) with some simplifications. There is no doubling down, splitting pairs, and insurance.

# Development

## Running the DAppChain

```
# Download the project
git clone https://github.com/loomnetwork/unity-evm-blackjack.git
cd unity-evm-blackjack

# Build the Truffle project. This will copy the ABI file to the Unity client,
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
