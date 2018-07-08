var fs = require("fs");

var BlackJack = artifacts.require("./BlackJack.sol");

module.exports = function(deployer) {
    console.log("Copying BlackJack.bin to Loom DAppChain instance");
    try {
        fs.mkdirSync("../dappchain/build/");
    } catch (err) {
        if (err.code !== "EEXIST") throw err;
    }
    fs.writeFileSync("../dappchain/build/BlackJack.bin", BlackJack.bytecode.substring(2), function(
        err
    ) {
        throw Error(err);
    });
};
