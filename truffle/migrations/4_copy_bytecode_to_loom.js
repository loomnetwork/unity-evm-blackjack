var fs = require("fs");
var Blackjack = artifacts.require("./Blackjack.sol");

module.exports = function(deployer) {
    console.log("Copying Blackjack.bin to Loom DAppChain instance");
    fs.existsSync("../dappchain/build/") || fs.mkdirSync("../dappchain/build/");
    fs.writeFileSync("../dappchain/build/Blackjack.bin", Blackjack.bytecode.substring(2));
};
