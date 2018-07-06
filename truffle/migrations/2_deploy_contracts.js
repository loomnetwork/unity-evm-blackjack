var BlackJack = artifacts.require("./BlackJack.sol");
var TestBlackJack = artifacts.require("./TestBlackJack.sol");
var DeckLibrary = artifacts.require("./libraries/DeckLibrary.sol");
var GameLibrary = artifacts.require("./libraries/GameLibrary.sol");

module.exports = function(deployer) {
  //deployer.deploy(DeckLibrary);
  //deployer.deploy(GameLibrary);
  //deployer.link(DeckLibrary, BlackJack);
  //deployer.link(GameLibrary, BlackJack);
  deployer.deploy(BlackJack);
  deployer.deploy(TestBlackJack);
};