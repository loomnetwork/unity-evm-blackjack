pragma solidity ^0.4.24;

library ArrayLibrary {
    function removeAddressFromArrayUnordered(address[] storage array, address item) internal {
        for(uint i = 0; i < array.length; i++) {
            if (array[i] == item) {
                address lastItem = array[array.length - 1];
                array[i] = lastItem;
                array.length--;
                return;
            }
        }

        revert("address not found");
    }
}