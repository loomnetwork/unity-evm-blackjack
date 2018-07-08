set -e

if [ ! -d ./build ]; then
    mkdir build
fi

cd build

if [ ! -f ./loom ]; then
    wget https://storage.googleapis.com/private.delegatecall.com/loom/linux/build-196/loom
    chmod +x loom
fi

rm -rf ./app.db
rm -rf ./chaindata


cp ../genesis.example.json genesis.json
set +e
./loom init
set -e
./loom run