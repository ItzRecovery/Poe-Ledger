const fs=require('node:fs'),assert=require('node:assert/strict');
const c=fs.readFileSync('ProjectPoeLedger/Web/index.html','utf8');
function extract(start,end){const i=c.indexOf(start),j=c.indexOf(end,i);assert(i>=0&&j>i,'Missing source anchor '+start);return c.slice(i,j)}
const parts=[extract('function calc(x)','function field('),extract('function calc338(','function simulate338('),extract('function atlasCalc33(','function atlasPage33('),extract('function beastCalc46(','function beastPage46('),extract('function norm27(','function visiblePobMods462('),extract('const known27=','function shop27('),extract('const nn338=','function ecoCatalog338('),extract('const mult=','let state='),extract('const keys29=','function applyAdvanced29'),extract('function gemColor542','function gemRows28'),extract('async function readSave545',"$('exportCompact545').onclick")];
const defs=c.match(/num=x=>Number\(x\)\|\|0,known=.*?,fmt=/)[0].slice(0,-5);
const testSource=String.raw`
let n=0;function test(name,fn){assert(fn(),name);console.log('PASS '+name);n++}
let boss={entry:[{price:20,q:2}],drops:[{on:true,p:50,price:200,q:1,g:'pool'},{on:false,p:100,price:100,q:1,g:'pool'}],extra:5,minutes:3};
test('boss expected profit',()=>calc(boss).net===55&&calc(boss).hour===1100&&calc(boss).complete);
test('missing boss price',()=>!calc({...boss,entry:[{price:null,q:2}]}).complete);
test('negative price',()=>!calc({...boss,entry:[{price:-1,q:2}]}).complete);
test('zero duration',()=>!calc({...boss,minutes:0}).complete);
let e={rows:[{p:50,price:100,qty:2}],cost:10,minutes:5,success:80};
test('strategy success-adjusted profit',()=>calc338(e).net===70&&calc338(e).hour===840);
test('missing success',()=>calc338({...e,success:''}).missing>0);
let b={rows:[{qty:4,price:30}],cost:20,orb:2,minutes:10,maps:5,other:5};
test('beast orb cost',()=>beastCalc46(b).net===97&&beastCalc46(b).complete);
test('missing beast price',()=>!beastCalc46({...b,rows:[{qty:1,price:''}]}).complete);
let a={rolling:'5',other:'2',items:[{name:'test',price:20,qty:1,lasts:4}],loot:[{price:40,qty:1}],completion:50,minutes:2};
test('atlas amortization',()=>atlasCalc33(a).net===8&&atlasCalc33(a).hour===240);
test('blank and boolean prices rejected',()=>!has338(' ')&&!has338(false)&&has338(0));
test('PoB life modifier',()=>affix27('Rarity: Rare\nExample\nVaal Regalia\nImplicits: 0\n+100 to maximum Life',[]).matched[0]?.value.min===100);
test('PoB implicit count',()=>affix27('Rarity: Rare\nExample\nRing\nImplicits: 1\n+20 to maximum Life',[{id:'implicit.fixture',text:'+# to maximum Life'}]).matched[0]?.id==='implicit.fixture');
test('Hybrid attribute modifiers',()=>stats29('+30 to Strength and Dexterity').str===30&&stats29('+30 to Strength and Dexterity').dex===30);
test('Percentage attributes excluded',()=>stats29('+10% to all Attributes').str===0);
test('Conditional resistance excluded',()=>stats29('+20% to Fire Resistance while stationary').fire===0);
test('Hybrid resistance modifiers',()=>stats29('+25% to Fire and Cold Resistances').cold===25);
test('Maximum resistance excluded',()=>stats29('+3% to maximum Fire Resistance').fire===0);
const gem=a=>({getAttribute:k=>a[k]??null});
test('Explicit gem colour',()=>gemColor542(gem({color:'G'}))==='green');
test('White gem colour',()=>gemColor542(gem({nameSpec:'Portal'}))==='white');
test('Unknown gem stays neutral',()=>gemColor542(gem({nameSpec:'Unknown'}))==='');
test('Single requirement gem colour',()=>gemColor542(gem({reqStr:'0',reqDex:'0',reqInt:'20'}))==='blue');
test('Ambiguous gem stays neutral',()=>gemColor542(gem({reqStr:'10',reqDex:'10',reqInt:'0'}))==='');
test('Unique numeric template',()=>matchAffix542(new Map([['explicit|socketed gems are supported by level # hypothermia',new Set(['explicit.fixture'])]]),'explicit','Socketed Gems are Supported by Level 20 Hypothermia')?.value.min===20);
test('Ambiguous trade stat rejected',()=>matchAffix542(new Map([['explicit|# to maximum life',new Set(['explicit.a','explicit.b'])]]),'explicit','+20 to maximum Life')===null);
console.log(n+' offline checks passed');
return readSave545;
`;
const readSave=new Function('assert','const state={rate:200,jackpot:10};const '+defs+';\n'+parts.join('\n')+'\n'+testSource)(assert);

(async()=>{const fixture={bosses:[{name:'Maven',entry:[],drops:[]}],features24:{history:{sample:[{time:123,price:42.5,rate:200,source:'Test'}]}}};const plain=new Blob([JSON.stringify(fixture)]);assert.deepEqual(await readSave(plain),fixture);const compressed=new Blob([require('node:zlib').gzipSync(JSON.stringify(fixture))]);assert.deepEqual(await readSave(compressed),fixture);await assert.rejects(()=>readSave(new Blob([new Uint8Array([31,139,0,0])])));console.log('PASS JSON and gzip save round trip; corrupt gzip rejected')})().catch(e=>{console.error(e);process.exitCode=1});
