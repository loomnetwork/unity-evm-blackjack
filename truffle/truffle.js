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

var LoomUnityBuildUtility = require("./LoomUnityBuildUtility");
module.exports = {
    // See <http://truffleframework.com/docs/advanced/configuration>
    // to customize your Truffle configuration!
    build: function(options, callback) {
        new LoomUnityBuildUtility(options, "Blackjack", "../unityclient/Assets/Blackjack/Assets/Contract/", "../dappchain/build/").copyFiles();
    },
    solc: {
        optimizer: {
            enabled: true,
            runs: 1
        }
    }
};
