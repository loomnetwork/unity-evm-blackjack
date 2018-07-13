/*
 * NB: since truffle-hdwallet-provider 0.0.5 you must wrap HDWallet providers in a
 * function when declaring them. Failure to do so will cause commands to hang. ex:
 * ```
 * mainnet: {
 *     provider: function() {
 *       return new HDWalletProvider(mnemonic, 'https://mainnet.infura.io/<infura-key>')
 *     },
 *     network_id: '1',
 *     gas: 4500000,
 *     gasPrice: 10000000000,
 *   },
 */

module.exports = {
    // See <http://truffleframework.com/docs/advanced/configuration>
    // to customize your Truffle configuration!
    build: function(options, callback) {
        var fs = require('fs');
        var json = JSON.parse(fs.readFileSync(options.destination_directory + "/contracts/Blackjack.json", 'utf8'));

        console.log("Copying Blackjack.abi to Unity client");
        fs.writeFileSync("../unityclient/Assets/Blackjack/Assets/Contract/Blackjack.abi.json", JSON.stringify(json.abi), function(err) {
            throw Error(err);
        });

        console.log("Copying Blackjack.bin to Loom DAppChain instance");
        fs.existsSync("../dappchain/build/") || fs.mkdirSync("../dappchain/build/");
        fs.writeFileSync("../dappchain/build/Blackjack.bin", json.bytecode.substring(2));
    },
    solc: {
        optimizer: {
            enabled: true,
            runs: 1
        }
    }
};
