'use strict';
/* Locked style + subject catalog. Edit STYLE to finetune every future asset.
   Do NOT mention checkerboards, white cards, or PNG alpha in the prompt —
   GPT Image will paint those if you name them. Transparency is an API flag. */

const STYLE = {
  camera: 'classic 2:1 dimetric isometric game sprite, camera 26 degrees, identical camera for every asset in the set',
  medium: 'chunky pixel-art industrial cel, visible pixels, hard two-band lighting plus one shade band, not photoreal, not path-traced CGI, not anime',
  outline: 'crisp 1px dark ink outline, clean readable silhouette, no glow halo, no colored fringe',
  light: 'key light from the upper-left, cool shade on the right faces',
  palette: '#0b0d11 ink, #232b36 hull, #6b8296 steel, #6f8f52 moss, #d9a13c amber lamps, #c05f3c rust, #4b83c2 cool shade, #e7eaef edge highlight — stay inside this palette',
  isolation: 'ONE object only, centered in the frame, fills most of the square, standing as a sprite, no environment around it, no other objects, no people, no letters, no watermark'
};

const CATALOG = {
  building: {
    label: 'Buildings',
    hint: 'city lots — one structure per image, many types',
    types: [
      { id: 'warehouse', name: 'Warehouse', body: 'compact two-storey corrugated steel warehouse, gabled roof, roll-up door, thin amber window band' },
      { id: 'tower', name: 'Tower', body: 'tall slender industrial tower / chimney stack, rectangular shaft, moss band, small amber beacon on top' },
      { id: 'tank', name: 'Storage tank', body: 'short wide cylindrical fuel tank, steel hoops, rust drum, one pipe valve' },
      { id: 'silo', name: 'Silo', body: 'tall grain/ore silo cylinder with conical cap and a side ladder' },
      { id: 'hangar', name: 'Hangar', body: 'wide low vehicle hangar, huge front shutter, steel ribs' },
      { id: 'crane', name: 'Yard crane', body: 'small gantry crane over a loading bay, lattice boom, rust feet' },
      { id: 'gatehouse', name: 'Gatehouse', body: 'checkpoint gatehouse, striped barrier arm, small booth with amber window' },
      { id: 'depot', name: 'Truck depot', body: 'two-bay truck depot, open stalls, corrugated roof, fuel pump' },
      { id: 'ruin', name: 'Ruin', body: 'ruined industrial shed, collapsed wall, standing corner, exposed ribs' },
      { id: 'refinery', name: 'Still', body: 'small refinery still: one column, two tanks, connecting pipes' },
      { id: 'water-tower', name: 'Water tower', body: 'stilted water tower, steel tank on four legs, rust staining' },
      { id: 'mast', name: 'Radio mast', body: 'lattice radio mast with a tiny equipment shack at the base' },
      { id: 'foundry', name: 'Foundry', body: 'foundry hall, brick-and-steel, one smoking stack, slag door' },
      { id: 'bunker', name: 'Bunker', body: 'low concrete bunker, slit windows, rust door' },
      { id: 'apartments', name: 'Block', body: 'small 4-storey worker housing block, steel balconies, amber windows' },
      { id: 'station', name: 'Station', body: 'tiny road station, platform canopy, steel posts, blank clock face' },
      { id: 'mill', name: 'Mill', body: 'small brick mill with one waterwheel or gear house, steel roof' },
      { id: 'cottage', name: 'Cottage', body: 'tiny worker cottage, dark roof, chimney, one amber window' },
      { id: 'chapel', name: 'Chapel', body: 'tiny industrial chapel, steel steeple, no readable sign' },
      { id: 'windmill', name: 'Windmill', body: 'small steel windmill / pump jack, lattice tower, four vanes' },
      { id: 'minehead', name: 'Mine head', body: 'mine headframe, two A-legs, winding wheel, tiny cage house' },
      { id: 'cooling', name: 'Cooling tower', body: 'small hyperbolic cooling tower, steel banding, no steam cloud filling the frame' },
      { id: 'barn', name: 'Goods barn', body: 'timber-and-steel goods barn, wide eaves, sliding door' },
      { id: 'watchtower', name: 'Watchtower', body: 'timber/steel watchtower, open top deck, ladder, amber lantern' },
      { id: 'dock', name: 'Dock crane', body: 'small dockside jib crane, counterweight, rust boom' },
      { id: 'office', name: 'Yard office', body: 'one-storey prefab yard office, flat roof, two amber windows, steps' },
      { id: 'smelter', name: 'Smelter', body: 'short smelter shed with one fat stack and a slag trough' },
      { id: 'pump', name: 'Pump house', body: 'small pump house, pipes bursting from one wall, rust valves' }
    ],
    flavors: [
      'corrugated metal siding', 'rust-orange base trim', 'amber interior light in windows',
      'hazard stripe on one door', 'pipes along one wall', 'rooftop vent cowls',
      'moss on the shady face', 'riveted plates', 'one dented panel'
    ]
  },
  rock: {
    label: 'Rocks',
    hint: 'combinable modules — ridge + boulder + peak stack into a mountain',
    types: [
      { id: 'pebble', name: 'Pebble', body: 'small faceted grey pebble, fist-sized, chunky polygonal stone' },
      { id: 'boulder', name: 'Boulder', body: 'large blocky boulder, flat-ish top and sides so other rocks can sit against it, moss patch on the lit face' },
      { id: 'ridge', name: 'Ridge module', body: 'long low mountain RIDGE MODULE: faceted grey rock slab with a flat top ledge designed so other rocks can stack on it — a combinable tile, not a finished mountain' },
      { id: 'peak', name: 'Peak cap', body: 'mountain PEAK CAP: small pointed grey spire meant to sit on top of other rock modules, narrower than a boulder' },
      { id: 'slab', name: 'Slab', body: 'low wide stone slab, stratified, one raised shoulder' },
      { id: 'mesa', name: 'Mesa', body: 'small mesa / butte, flat top, talus as part of the rock mass' },
      { id: 'arch', name: 'Arch', body: 'stone arch / hoodoo, one opening, stacked blocks' },
      { id: 'scree', name: 'Scree pile', body: 'tight pile of 3–5 small angular stones, one silhouette, combinable clutter' },
      { id: 'split', name: 'Split pair', body: 'two touching standing stones, one taller, shared rock base' },
      { id: 'cliff', name: 'Cliff face', body: 'short vertical cliff module, stratified, combinable wall of rock' },
      { id: 'cairn', name: 'Cairn', body: 'stacked cairn of 4 angular stones, one silhouette' },
      { id: 'spike', name: 'Spike', body: 'single sharp standing stone spike, narrow footprint' }
    ],
    flavors: [
      'cool steel-grey stone', 'moss on the upper-left face', 'visible strata lines',
      'sharp faceted planes', 'dark cracks', 'one rust-stained streak'
    ]
  },
  vegetation: {
    label: 'Vegetation',
    hint: 'trees and scrub — same camera, no grass tile',
    types: [
      { id: 'pine', name: 'Pine', body: 'conical moss-green pine, dark bark trunk, chunky canopy bands' },
      { id: 'broad', name: 'Broadleaf', body: 'round clumped broadleaf tree, moss canopy, short dark trunk' },
      { id: 'dead', name: 'Dead tree', body: 'leafless dead tree, two branches, pale bark' },
      { id: 'stump', name: 'Stump', body: 'cut stump with a few roots' },
      { id: 'bush', name: 'Bush', body: 'low moss bush, round, no trunk' },
      { id: 'log', name: 'Log', body: 'fallen log, bark corrugation, one cut end' },
      { id: 'cypress', name: 'Cypress', body: 'tall narrow cypress, dark green, slim trunk' },
      { id: 'orchard', name: 'Orchard tree', body: 'small orchard tree, round canopy, short trunk' },
      { id: 'reed', name: 'Reeds', body: 'tight clump of reeds / tall grass as one silhouette, no ground patch' }
    ],
    flavors: [
      'moss #6f8f52 canopy', 'cool shade on the right', 'slightly uneven silhouette', 'sparse rust leaves'
    ]
  },
  prop: {
    label: 'Props',
    hint: 'crates, pylons, wrecks — scatter and roadside',
    types: [
      { id: 'crate', name: 'Crate', body: 'steel cargo crate, rivets, one amber stencil mark, no readable letters' },
      { id: 'container', name: 'Container', body: 'short shipping container, rust corners, closed doors' },
      { id: 'pylon', name: 'Pylon', body: 'electrical pylon, lattice, tiny transformer box' },
      { id: 'wreck', name: 'Wreck', body: 'gutted truck wreck, no wheels, rust hull' },
      { id: 'barrels', name: 'Barrels', body: 'two steel drums stacked, rust rings' },
      { id: 'lamp', name: 'Lamp', body: 'road lamp on a bent pole, amber bulb' },
      { id: 'fence', name: 'Fence', body: 'short industrial fence segment, three posts, chain or plate' },
      { id: 'sign', name: 'Blank sign', body: 'road sign on a pole, blank rusty face, no letters' },
      { id: 'pallet', name: 'Pallet', body: 'wooden pallet with two crates on it' },
      { id: 'pipes', name: 'Pipe stack', body: 'stack of three steel pipes, rust ends' },
      { id: 'fuelpump', name: 'Fuel pump', body: 'old fuel pump, hose, rust panel, no letters' },
      { id: 'barrier', name: 'Barrier', body: 'striped road barrier, two feet' },
      { id: 'generator', name: 'Generator', body: 'small diesel generator box, exhaust pipe, rust base' },
      { id: 'antenna', name: 'Dish', body: 'small satellite dish on a short mast' }
    ],
    flavors: [
      'rust #c05f3c staining', 'amber lamp', 'rivets', 'one missing panel'
    ]
  },
  vehicle: {
    label: 'Vehicles',
    hint: 'convoy pieces — one vehicle, facing upper-right',
    types: [
      { id: 'truck', name: 'Cargo truck', body: 'small cargo truck, steel chassis, amber cab windows, rust hubs, facing upper-right' },
      { id: 'mule', name: 'Mule', body: 'boxy cargo mule, no cab, short wheelbase, facing upper-right' },
      { id: 'tanker', name: 'Tanker', body: 'small tanker truck, cylindrical tank, facing upper-right' },
      { id: 'tractor', name: 'Yard tractor', body: 'short yard tractor, no trailer, facing upper-right' },
      { id: 'hauler', name: 'Ore hauler', body: 'short dump-bed ore hauler, facing upper-right' },
      { id: 'van', name: 'Panel van', body: 'small steel panel van, amber windshield, facing upper-right' }
    ],
    flavors: [
      'steel hull', 'amber cab light', 'rust wheel hubs', 'one crate on the bed'
    ]
  }
};

function pick(arr, rnd) {
  return arr[Math.floor(rnd() * arr.length)];
}

function mulberry(seed) {
  let s = seed >>> 0 || 1;
  return () => {
    s += 0x6D2B79F5;
    let t = Math.imul(s ^ (s >>> 15), 1 | s);
    t ^= t + Math.imul(t ^ (t >>> 7), 61 | t);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

function styleBlock() {
  return [
    '2D isometric game sprite.',
    STYLE.camera + '.',
    STYLE.medium + '.',
    STYLE.outline + '.',
    STYLE.light + '.',
    'Palette: ' + STYLE.palette + '.',
    STYLE.isolation + '.'
  ].join(' ');
}

function assemble(opts) {
  const cat = CATALOG[opts.category];
  if (!cat) return '';
  const type = cat.types.find((t) => t.id === opts.typeId) || cat.types[0];
  const rnd = mulberry(opts.seed || (Math.random() * 1e9) | 0);
  const flavors = [];
  const n = opts.flavorCount == null ? 2 : opts.flavorCount;
  const pool = cat.flavors.slice();
  for (let i = 0; i < n && pool.length; i++) {
    const ix = Math.floor(rnd() * pool.length);
    flavors.push(pool.splice(ix, 1)[0]);
  }
  const extra = (opts.extra || '').trim();
  const subject = 'SUBJECT: ' + type.body + (flavors.length ? ', ' + flavors.join(', ') : '') + '.';
  const bits = [styleBlock(), subject];
  if (extra) bits.push('NOTES: ' + extra);
  return bits.join('\n\n');
}

function randomType(category, seed) {
  const cat = CATALOG[category];
  const rnd = mulberry(seed || (Math.random() * 1e9) | 0);
  return pick(cat.types, rnd);
}

function randomCategory(seed) {
  const keys = Object.keys(CATALOG);
  return pick(keys, mulberry(seed || (Math.random() * 1e9) | 0));
}

if (typeof window !== 'undefined') {
  window.ArtPrompt = { STYLE, CATALOG, styleBlock, assemble, randomType, randomCategory, mulberry };
}
