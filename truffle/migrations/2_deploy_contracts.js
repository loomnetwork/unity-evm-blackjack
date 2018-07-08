var BlackJack = artifacts.require("./BlackJack.sol");
var TestingBlackJack = artifacts.require("./TestingBlackJack.sol");

module.exports = function(deployer) {
    deployer.deploy(BlackJack);
    deployer.deploy(TestingBlackJack);
};