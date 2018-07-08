var fs = require('fs');

var BlackJack = artifacts.require("./BlackJack.sol");

module.exports = function(deployer) {
    console.log("Copying BlackJack.abi to Unity client");
    fs.writeFileSync("../unityclient/Assets/BlackJack/Assets/Contract/BlackJack.abi.json", JSON.stringify(BlackJack.abi), function(err) {
        throw Error(err);
    });
};