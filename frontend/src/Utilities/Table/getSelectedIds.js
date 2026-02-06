import _ from 'lodash';

// ⚡ Bolt: Use WeakMap to cache selected IDs by selectedState object reference.
// This optimizes re-renders where the selection state hasn't changed,
// reducing O(N) iteration to O(1).
const cache = new WeakMap();

function getSelectedIds(selectedState, { parseIds = true } = {}) {
  if (!selectedState || typeof selectedState !== 'object') {
    return [];
  }

  let stateCache = cache.get(selectedState);
  if (stateCache && stateCache.has(parseIds)) {
    return stateCache.get(parseIds);
  }

  const result = _.reduce(
    selectedState,
    (res, value, id) => {
      if (value) {
        const parsedId = parseIds ? parseInt(id) : id;

        res.push(parsedId);
      }

      return res;
    },
    []
  );

  if (!stateCache) {
    stateCache = new Map();
    cache.set(selectedState, stateCache);
  }
  stateCache.set(parseIds, result);

  return result;
}

export default getSelectedIds;
