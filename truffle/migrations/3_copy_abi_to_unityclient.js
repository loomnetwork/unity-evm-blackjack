var fs = require('fs');
var Blackjack = artifacts.require("./Blackjack.sol");

module.exports = function(deployer) {
    console.log("Copying Blackjack.abi to Unity client");
    fs.writeFileSync("../unityclient/Assets/Blackjack/Assets/Contract/Blackjack.abi.json", JSON.stringify(Blackjack.abi), function(err) {
        throw Error(err);
    });
};