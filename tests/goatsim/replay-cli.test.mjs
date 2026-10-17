import test from 'node:test';
import assert from 'node:assert/strict';
import {mkdtemp,writeFile,readFile,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {main} from '../../labs/goatsim/replay.mjs';
const fixture=()=>({schemaVersion:1,kind:'capture-conditioned-replay',contextId:'synthetic-cli-test',className:'Synthetic',durationMs:1000,models:[{id:'hit',skill:'Synthetic',meanObservedHit:100,trainingHits:5}],timeline:[{atMs:0,modelId:'hit'}],observed:{coveredDamage:100,totalDamage:100,coveredHits:1,totalHits:1},restrictions:{allowGearScaling:false,allowNewRotation:false,independentValidation:false}});
test('private replay CLI produces qualified report and never overwrites',async()=>{
 const dir=await mkdtemp(join(tmpdir(),'goatsim-replay-'));try{
  const input=join(dir,'input.json'),output=join(dir,'report.json');await writeFile(input,JSON.stringify(fixture()));
  await main([input,output]);const text=await readFile(output,'utf8'),r=JSON.parse(text);
  assert.equal(r.predictedCoveredDamage,100);assert.equal(r.gameFormulaValidated,false);assert.equal(r.allowNewRotation,false);assert.equal(r.coveredHitWeightedAbsoluteErrorPercent,null);
  await assert.rejects(main([input,output]),{code:'EEXIST'});assert.equal(await readFile(output,'utf8'),text);
 }finally{await rm(dir,{recursive:true,force:true});}
});
test('private replay CLI rejects raw logs, oversize inputs and missing arguments',async()=>{
 const dir=await mkdtemp(join(tmpdir(),'goatsim-replay-'));try{
  const input=join(dir,'input.json'),output=join(dir,'report.json');await writeFile(input,'not a replay JSON');await assert.rejects(main([input,output]));
  await writeFile(input,'x'.repeat(1024*1024+1));await assert.rejects(main([input,output]),/1 MiB/);
  await assert.rejects(readFile(output),{code:'ENOENT'});await assert.rejects(main([]),/Usage/);
 }finally{await rm(dir,{recursive:true,force:true});}
});
