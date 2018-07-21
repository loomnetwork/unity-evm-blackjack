const fs = require('fs');
const path = require('path');

function mkdirSyncRecursive(targetDir) {
    if (!fs.existsSync(targetDir)) {
        for (var i = targetDir.length - 2; i >= 0; i--) {
            if (targetDir.charAt(i) == '/' || targetDir.charAt(i) == path.sep) {
                mkdirSyncRecursive(targetDir.slice(0, i));
                break;
            }
        }
        try {
            fs.mkdirSync(targetDir);
            return true;
        } catch (err) {
            if (err.code !== 'EEXIST') throw err;
        }
    }
    return false;
}

function LoomUnityBuildUtility(options, contracts, unityAbiDirectory, loomBinDirectory) {
    if (typeof contracts == "string") contracts = [contracts];
    if (!(contracts instanceof Array)) contracts = [];
    if (options.searchContractsInDestinationDirectory) {
        const buildDir = path.join(options.destination_directory, "contracts");
        const files = fs.readdirSync(buildDir);
        files.forEach(element => {
            if (path.extname(element) == ".json") {
                contracts.push(path.basename(element, ".json"));
            }
        });
    }

    // Make unique
    contracts = [...new Set(contracts)];

    this.options = options;
    this.contracts = contracts;
    this.unityAbiDirectory = unityAbiDirectory;
    this.loomBinDirectory = loomBinDirectory;
}

LoomUnityBuildUtility.prototype.copyFiles = function () {
    this.contracts.forEach(contractName => {
        var json = JSON.parse(fs.readFileSync(path.join(this.options.destination_directory, "contracts", contractName + ".json"), 'utf8'));

        var unityAbiPath = path.join(this.unityAbiDirectory, contractName + ".abi.json");
        console.log(`Copying ${contractName}.abi to Unity client (${unityAbiPath})`);
        fs.existsSync(this.unityAbiDirectory) || mkdirSyncRecursive(this.unityAbiDirectory);
        fs.writeFileSync(unityAbiPath, JSON.stringify(json.abi), function (err) {
            throw Error(err);
        });

        var loomBinPath = path.join(this.loomBinDirectory, contractName + ".bin");
        console.log(`Copying ${contractName}.bin to Loom DAppChain instance (${loomBinPath})`);
        fs.existsSync(this.loomBinDirectory) || mkdirSyncRecursive(this.loomBinDirectory);
        fs.writeFileSync(loomBinPath, json.bytecode.substring(2));
    });
}

module.exports = LoomUnityBuildUtility;