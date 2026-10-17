/** Descriptive paired trial report only; no optimality or causal improvement claim. */
export function compareTrials(trials,{minimumPairs=5}={}){
 if(!Array.isArray(trials)||!Number.isInteger(minimumPairs)||minimumPairs<2)throw new Error('Invalid trials');
 const pairs=new Map();let scenario;
 for(const t of trials){
  if(!t||typeof t.pairId!=='string'||!t.pairId||!['controlled','berdugz'].includes(t.candidate)||typeof t.scenarioFingerprint!=='string'||!t.scenarioFingerprint||t.durationSeconds!==120||!Number.isFinite(t.targetDamage)||t.targetDamage<0||t.captureComplete!==true||t.stationary!==true||t.targetCount!==1||t.fullLayoutConfirmed!==true)throw new Error('Incomplete or unmatched dummy trial');
  if(scenario===undefined)scenario=t.scenarioFingerprint;if(t.scenarioFingerprint!==scenario)throw new Error('Scenario changed: separate gear, buffs, patch, target, delay and loadout comparisons');
  const p=pairs.get(t.pairId)??{};if(p[t.candidate])throw new Error('Duplicate trial in pair');p[t.candidate]=t;pairs.set(t.pairId,p);
 }
 const values=[];
 for(const [pairId,p] of pairs){if(!p.controlled||!p.berdugz)throw new Error('Incomplete pair');values.push({pairId,controlledDps:p.controlled.targetDamage/120,berdugzDps:p.berdugz.targetDamage/120,differenceDps:(p.berdugz.targetDamage-p.controlled.targetDamage)/120});}
 const mean=xs=>xs.reduce((a,b)=>a+b,0)/xs.length;
 const differences=values.map(p=>p.differenceDps);
 return {status:values.length>=minimumPairs?'descriptive-comparison':'insufficient-trials',pairs:values,meanDifferenceDps:values.length?mean(differences):null,minDifferenceDps:values.length?Math.min(...differences):null,maxDifferenceDps:values.length?Math.max(...differences):null,benchmarkValidated:false,note:'User-entered trial summary; not independent validation, statistical significance, or proof of an optimal macro.'};
}
