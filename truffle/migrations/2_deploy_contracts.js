var Blackjack = artifacts.require("./Blackjack.sol");
var TestingBlackjack = artifacts.require("./TestingBlackjack.sol");

module.exports = function(deployer) {
    deployer.deploy(Blackjack);
    deployer.deploy(TestingBlackjack);
};