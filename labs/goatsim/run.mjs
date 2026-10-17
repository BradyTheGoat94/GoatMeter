import {readFile,writeFile} from 'node:fs/promises';
import {pathToFileURL} from 'node:url';
import {simulate} from './engine.mjs';
export function report(config){
 const r=simulate(config);const summary=Object.entries(r.counts).map(([skill,casts])=>({skill,casts}));
 return {warning:'SYNTHETIC TIMING MODEL — NOT AION 2 DPS',model:r.model,verifiedGameMechanics:false,durationMs:r.durationMs,summary,skippedManualInputs:r.timeline.filter(e=>e.skipped).length,remainingResource:r.resource,timeline:r.timeline,limitations:['No damage formula or class coefficients','No verified Global timings','No proc windows, latency, charge stages or Skill Queue','Cast counts do not establish optimal DPS']};
}
export async function main(args){
 const [input,output]=args;
 if(!input||args.length>2)throw new Error('Usage: node labs/goatsim/run.mjs input.json [output.json]');
 const raw=await readFile(input);if(raw.byteLength>1024*1024)throw new Error('Input limit: 1 MiB');
 const result=JSON.stringify(report(JSON.parse(raw)),null,2)+'\n';
 if(output)await writeFile(output,result,{flag:'wx'});else process.stdout.write(result);
}
if(process.argv[1]&&import.meta.url===pathToFileURL(process.argv[1]).href){main(process.argv.slice(2)).catch(e=>{console.error(e.message);process.exitCode=1;});}
