import {simulate} from './engine.mjs';
/** Same mechanics/input scenario, different decision policy. No damage or human-performance claim. */
export function compareModes(config){
 const macro=simulate({...config,executionMode:'macro'});
 const apl=simulate({...config,executionMode:'apl'});
 const names=[...new Set([...Object.keys(macro.counts),...Object.keys(apl.counts)])].sort();
 return {warning:'Synthetic cast comparison only — no game DPS or optimality claim',macro,apl,skills:names.map(skill=>({skill,macroCasts:macro.counts[skill]??0,aplCasts:apl.counts[skill]??0}))};
}
