/* K3 isometric art: builders, species, ground. Ported from k3-game-demo/engine/map.js. */
/* ==========================================================================
   2 · BUILDERS — seed -> volume[]. This is the whole art budget for scenery.
   Every builder is 3-12 lines because it emits data, not draw calls.
   ========================================================================== */
/* ROCK — five structural forms. Topology first: a stack, a slab and a hoodoo are
   different objects; twelve jitters of one blob are the same object. */
function bRock(R,r,B){
  const v=[],f=R(),MR={mat:'rock'};
  if(f<.28){                                  /* blob + chips */
    v.push(V_(ngon(R,8,r,.58,.86),0,r*(.60+R()*.55),B.p1,{mat:'rock',taper:.86}));
    if(R()<.75){const a=R()*TAU,d=r*.78;
      v.push(V_(shift(ngon(R,6,r*.44,.62,.9),Math.cos(a)*d,Math.sin(a)*d*.72),
        0,r*(.28+R()*.28),B.p1,{mat:'rock',taper:.80}));}
    if(R()<.40){const a=R()*TAU,d=r*.55;
      v.push(V_(shift(ngon(R,5,r*.30,.5,.9),Math.cos(a)*d,Math.sin(a)*d*.7),0,r*.2,B.p2,MR));}
  }else if(f<.48){                            /* balanced stack */
    let z=0,rr=r*.95;
    for(let i=0;i<2+(R()*2|0);i++){
      const h=rr*(.5+R()*.45);
      v.push(V_(shift(ngon(R,7,rr,.5,.86),(R()-.5)*r*.30,(R()-.5)*r*.22),
        z,z+h,i&1?B.p2:B.p1,{mat:'rock',taper:.78,lum:i*.04}));
      z+=h*.90;rr*=.70+R()*.10;
    }
  }else if(f<.66){                            /* low slab, one shoulder */
    v.push(V_(ngon(R,6,r*1.32,.32,.50),0,r*(.24+R()*.20),B.p1,{mat:'rock',taper:.76}));
    v.push(V_(shift(ngon(R,5,r*.50,.40,.60),(R()-.5)*r,0),0,r*(.34+R()*.26),B.p2,{mat:'rock',taper:.70}));
  }else if(f<.85){                            /* hoodoo / spire */
    v.push(V_(ngon(R,7,r*.72,.42,.9),0,r*(.28+R()*.20),B.p1,{mat:'rock',taper:.80}));
    v.push(V_(ngon(R,6,r*.46,.36,.9),r*.24,r*(1.3+R()*1.3),B.p1,{mat:'rock',taper:.38,face:'strata'}));
    if(R()<.5)v.push(V_(ngon(R,7,r*.40,.40,.9),r*(1.3+R()*.5),r*(1.6+R()*.6),B.p2,{mat:'rock',taper:.60}));
  }else{                                      /* split pair */
    for(const s of[-1,1])
      v.push(V_(shift(ngon(R,6,r*.52,.40,.86),s*r*.44,(R()-.5)*r*.2),
        0,r*(.55+R()*.55),B.p1,{mat:'rock',taper:.72}));
  }
  return v;
}
/* MESA / MOUNTAIN — three profiles (mesa · peak · butte) + a talus skirt. */
function bMesa(R,r,B){
  const v=[],f=R();
  const tiers = f<.40 ? 3+(R()*3|0) : f<.72 ? 4+(R()*3|0) : 2+(R()*2|0);
  const tp    = f<.40 ? .80         : f<.72 ? .62         : .92;
  const fall  = f<.72 ? .70 : .84;
  let rr=r,z=0,ox=0,oy=0;
  for(let i=0;i<tiers;i++){
    const hh=r*(.30+R()*.26)*(1-i*.06);
    v.push(V_(shift(ngon(R,9,rr,.28,.82),ox,oy),z,z+hh,i?B.p2:B.p1,
      {mat:'rock',taper:tp,lum:i*.05,face:'strata'}));
    z+=hh*.92;rr*=fall+R()*.12;ox+=(R()-.5)*r*.22;oy+=(R()-.5)*r*.16;
  }
  for(let i=0;i<3+(R()*4|0);i++){             /* talus boulders at the foot */
    const a=R()*TAU,d=r*(.82+R()*.5);
    v.push(V_(shift(ngon(R,6,r*(.10+R()*.13),.5,.85),Math.cos(a)*d,Math.sin(a)*d*.75),
      0,r*(.08+R()*.14),B.p2,{mat:'rock',taper:.70}));
  }
  if(B.cap)v.push(V_(shift(ngon(R,8,rr*1.08,.26,.82),ox,oy),z-2,z+r*.11,'ink-2',
    {mat:'ice',taper:.55,lum:.12}));
  return v;
}
/* TREE — broadleaf · twin-trunk · stump · fallen log. Canopy caps get `clump`
   texture, trunks get bark corrugation. */
function bTree(R,r,B){
  const v=[],f=R();
  if(f<.09){                                  /* stump */
    v.push(V_(ngon(R,7,r*.30,.22,.9),0,r*.32,B.p3,{mat:'bark',taper:.88}));
    v.push(V_(ngon(R,7,r*.22,.3,.9),r*.30,r*.34,B.p3,{mat:'rock',lum:-.1,edge:0}));
    return v;
  }
  if(f<.18){                                  /* fallen log */
    const a=R()*PI;
    v.push(V_(rot(bx(r*2.1,r*.32),a),0,r*.30,B.p3,{mat:'bark',taper:.9,face:'corrug'}));
    v.push(V_(shift(rot(bx(r*.46,r*.46),a),Math.cos(a)*r*1.0,Math.sin(a)*r*.8),
      0,r*.34,B.p3,{mat:'bark',taper:.8}));
    return v;
  }
  const twin=f>.87, th=r*(1.05+R()*.80);
  for(let k=0;k<(twin?2:1);k++)
    v.push(V_(shift(ngon(R,6,r*.13,.16,.9),twin?(k?r*.22:-r*.22):0,0),
      0,th*(.55+R()*.14),B.p3,{mat:'bark',taper:.72,edge:0}));
  const n=3+(R()*4|0);
  for(let i=0;i<n;i++){
    const a=R()*TAU,d=r*(.06+R()*.40),rr=r*(.48+R()*.46),z=th*(.48+R()*.38);
    v.push(V_(shift(ngon(R,10,rr,.24,.90),Math.cos(a)*d,Math.sin(a)*d*.8),
      z,z+rr*(.42+R()*.36),i&1?B.p1:B.p2,{mat:'foliage',taper:.62,lum:i*.03,edge:0}));
  }
  return v;
}
function bPine(R,r,B){
  const v=[];const th=r*(1.5+R()*1.2);
  v.push(V_(ngon(R,6,r*.11,.14,.9),0,th*.30,B.p3,{mat:'bark',edge:0}));
  const n=3+(R()*3|0);
  for(let i=0;i<n;i++){
    const f=i/n, z=th*(.20+f*.66);
    v.push(V_(ngon(R,9,r*(.64-f*.36),.20,.88),z,z+th*(.28-f*.06),i&1?B.p2:B.p1,
      {mat:'foliage',taper:.32,edge:0,lum:f*.06}));
  }
  return v;
}
function bDead(R,r,B){
  const v=[V_(ngon(R,5,r*.17,.30,.9),0,r*(1.1+R()*.9),B.p3,{mat:'bark',taper:.35})];
  for(let i=0;i<2+(R()*4|0);i++){
    const a=R()*TAU,d=r*(.15+R()*.32);
    v.push(V_(shift(ngon(R,4,r*.09,.4,.9),Math.cos(a)*d,Math.sin(a)*d*.7),
      r*(.5+R()*.7),r*(.9+R()*.9),B.p3,{mat:'bark',taper:.20,edge:0}));
  }
  if(R()<.4)v.push(V_(ngon(R,7,r*.34,.4,.85),0,r*.14,B.p3,{mat:'rock',taper:.7,lum:-.1}));
  return v;
}
/* BUILDING — the ASSET-FORGE BD_SLOT grammar expressed in the volume IR.
   8 foot x 4 size x 8 roof x 5 face x 5 use x 4 wear = 25,600 structures.
   `foot` RESTRUCTURES the plan (courtyard, podium+tower, split volumes) rather
   than scaling one box — the SHIP-PIPELINE §5 fix for the 24/48 twin rate. */
const BD={
  foot:['square','long','L','court','podium','split','round','tower'],
  size:['low','mid','high','spire'],
  roof:['flat','hvac','helipad','antenna','dome','hangar','saw','tanks'],
  face:['bands','grid','blank','glass','buttress'],
  use :['residential','industrial','military','agri','command'],
  wear:['intact','intact','scarred','holed','gutted']
};
const USE={                                   /* use locks palette + lit density */
  residential:{c:'steel', lit:'amber', d:.30},
  industrial :{c:'amber', lit:'amber', d:.12},
  military   :{c:'rust',  lit:'rust',  d:.06},
  agri       :{c:'moss',  lit:'moss',  d:.08},
  command    :{c:'azure', lit:'azure', d:.22}
};
const SIZEH={low:[16,34],mid:[38,40],high:[74,36],spire:[118,26]};

function roofKit(v,R,kind,w,d,tz,B,U){
  if(kind==='hvac'){
    for(let i=0;i<2+(R()*3|0);i++)
      v.push(V_(bx(w*.20,d*.20,(R()-.5)*w*.55,(R()-.5)*d*.55),tz,tz+w*(.10+R()*.16),
        B.p3,{mat:'metal'}));
  }else if(kind==='helipad'){
    v.push(V_(ngon(R,14,w*.36,.02,.92),tz,tz+1.8,'steel',{mat:'metal',lum:.10}));
    v.push(V_(ngon(R,14,w*.24,.02,.92),tz+1.8,tz+2.2,U.c,{lum:.22,edge:0}));
  }else if(kind==='antenna'){
    v.push(V_(ngon(R,4,w*.05,.05,.9),tz,tz+w*(1.1+R()*1.3),B.p3,{mat:'metal',taper:.15,edge:0}));
    for(let i=1;i<=2;i++)v.push(V_(bx(w*(.34-i*.09),w*.04),tz+w*(.4*i),tz+w*(.4*i)+1.4,B.p3,{edge:0}));
    v.push(V_(ngon(R,6,w*.045,.1,.9),tz+w*1.1,tz+w*1.2,'rust',{lum:.5,edge:0,emit:w*.5}));
  }else if(kind==='dome'){
    for(let i=0;i<3;i++)
      v.push(V_(ngon(R,12,w*(.40-i*.10),.03,.92),tz+i*w*.09,tz+(i+1)*w*.09,B.p2,
        {mat:'metal',taper:.86}));
  }else if(kind==='hangar'){
    v.push(V_(bx(w*.94,d*.88),tz,tz+w*.30,B.p2,{mat:'metal',taper:.90,face:'corrug'}));
  }else if(kind==='saw'){
    for(let i=0;i<3;i++)
      v.push(V_(bx(w*.24,d*.82,(i-1)*w*.30,0),tz,tz+w*.17,B.p3,{mat:'metal',taper:.40}));
  }else if(kind==='tanks'){
    for(let i=0;i<2+(R()*2|0);i++)
      v.push(V_(shift(ngon(R,10,w*.14,.04,.92),(R()-.5)*w*.45,(R()-.5)*d*.4),
        tz,tz+w*(.28+R()*.22),B.p2,{mat:'metal'}));
  }
}
function bBldg(R,r,B){
  const P_=a=>a[(R()*a.length)|0];
  let foot=P_(BD.foot), size=P_(BD.size);
  const roof=P_(BD.roof), fac=P_(BD.face), use=P_(BD.use), wear=P_(BD.wear);
  if(foot==='tower'&&(size==='low'||size==='mid'))size='high';   /* coherence rules */
  if(roof==='hangar')size='low';
  const U=USE[use], sh=SIZEH[size], k=r/34;
  let H=sh[0]*k, w=sh[1]*k, d=w*(.70+R()*.5);
  if(wear==='gutted')H*=.55; else if(wear==='holed')H*=.78;
  const faceK = fac==='glass'?'win' : fac==='grid'?'rivet' : fac==='blank'?null
              : fac==='buttress'?'corrug' : 'bands';
  const mat   = fac==='glass'?'glass' : fac==='grid'?'metal' : 'concrete';
  const wr    = wear==='intact'?0 : wear==='scarred'?.45 : wear==='holed'?.75 : 1;
  const v=[];
  const body=(px,py,pw,pd,z0,z1,c)=>v.push(V_(bx(pw,pd,px,py),z0,z1,c||U.c,
    {mat:mat,face:faceK,lit:fac==='blank'?null:U.lit,litD:U.d,wear:wr}));

  if(foot==='square')      body(0,0,w,d,0,H);
  else if(foot==='long')   body(0,0,w*1.9,d*.68,0,H*.72);
  else if(foot==='L'){     body(-w*.40,0,w*.80,d,0,H);
                           body(w*.45,d*.24,w*.90,d*.52,0,H*.70); }
  else if(foot==='court'){ body(0,-d*.72,w*1.6,d*.46,0,H*.80);
                           body(0, d*.72,w*1.6,d*.46,0,H*.80);
                           body(-w*.78,0,w*.44,d*.98,0,H*.80);
                           body( w*.78,0,w*.44,d*.98,0,H*.80); }
  else if(foot==='podium'){body(0,0,w*1.7,d*1.5,0,H*.28);
                           body(0,0,w*.78,d*.68,H*.26,H*1.15); }
  else if(foot==='split'){ body(-w*.55,0,w*.72,d,0,H);
                           body( w*.55,0,w*.72,d,0,H*.80);
                           v.push(V_(bx(w*.52,d*.30),H*.58,H*.66,U.c,
                             {mat:'metal',face:'grate'})); }        /* sky bridge */
  else if(foot==='round')  v.push(V_(ngon(R,12,w*.62,.04,.92),0,H,col(U.c),
                             {mat:mat,face:faceK,lit:fac==='blank'?null:U.lit,litD:U.d,wear:wr}));
  else {                   body(0,0,w*1.5,d*1.4,0,H*.22);           /* tower */
                           body(0,0,w*.54,d*.54,H*.20,H*1.4); }

  let tallest=v[0];for(const q of v)if(q.z1>tallest.z1)tallest=q;
  roofKit(v,R,roof,w,d,tallest.z1,B,U);
  if(wr>.6)for(let i=0;i<2+(R()*3|0);i++){                          /* collapse rubble */
    const a=R()*TAU,dd=w*(.7+R()*.6);
    v.push(V_(shift(ngon(R,5,w*(.08+R()*.10),.5,.8),Math.cos(a)*dd,Math.sin(a)*dd*.75),
      0,w*(.05+R()*.10),B.p2,{mat:'concrete',taper:.7}));
  }
  return v;
}
/* RUIN — three collapse states: standing wall run · corner shell · rubble mound */
function bRuin(R,r,B){
  const v=[],f=R(),a=R()*TAU;
  if(f<.55){                                   /* wall run with broken teeth */
    const n=3+(R()*3|0), seg=r*2.0/n;
    for(let i=0;i<n;i++){
      const tp=(i-(n-1)/2)*seg, h=r*(.30+R()*1.45);
      if(h<r*.2)continue;
      v.push(V_(shift(rot(bx(seg*.9,r*.3),a),Math.cos(a)*tp,Math.sin(a)*tp*.85),
        0,h,B.p2,{mat:'concrete',face:'bands',wear:.8}));
    }
  }else if(f<.82){                             /* corner shell — two walls meeting */
    for(const s of[0,1]){
      const aa=a+s*PI/2, ln=r*(1.1+R()*.7);
      v.push(V_(shift(rot(bx(ln,r*.28),aa),Math.cos(aa+PI/2)*r*.5,Math.sin(aa+PI/2)*r*.4),
        0,r*(.8+R()*1.2),B.p2,{mat:'concrete',face:'win',lit:null,wear:.9}));
    }
  }else{                                       /* pancaked floor slabs */
    let z=0;
    for(let i=0;i<3+(R()*3|0);i++){
      const h=r*.09;
      v.push(V_(shift(ngon(R,6,r*(.9-i*.08),.22,.7),(R()-.5)*r*.3,(R()-.5)*r*.2),
        z,z+h,B.p2,{mat:'concrete',taper:.94,wear:.6}));
      z+=h*(1.6+R());
    }
  }
  for(let i=0;i<3+(R()*4|0);i++){              /* exposed rebar */
    const x=(R()-.5)*r*1.7,y=(R()-.5)*r*.6,h=r*(.4+R()*1.2);
    v.push(V_(bx(1.5,1.5,x,y),h*.65,h,'ink-4',{edge:0}));
  }
  return v;
}
function bCrate(R,r,B){
  const v=[],f=R();
  if(f<.30){                                   /* drum cluster */
    for(let i=0;i<2+(R()*3|0);i++){
      const a=R()*TAU,d=r*R()*.7;
      v.push(V_(shift(ngon(R,10,r*.34,.04,.92),Math.cos(a)*d,Math.sin(a)*d*.75),
        0,r*(.9+R()*.5),i&1?B.p2:B.g,{mat:'metal',face:'bands'}));
    }
    return v;
  }
  v.push(V_(bx(r*1.5,r*1.15),0,r*(.8+R()*.7),B.p2,{mat:'metal',face:'rivet'}));
  v.push(V_(bx(r*1.36,r*1.02),v[0].z1,v[0].z1+r*.10,B.g,{mat:'metal',lum:.08}));
  if(R()<.45)v.push(V_(bx(r*1.15,r*.86,(R()-.5)*r*.3,(R()-.5)*r*.25),
    v[0].z1+r*.10,v[0].z1+r*(.7+R()*.5),B.p3,{mat:'metal',face:'seam'}));
  return v;
}
function bPylon(R,r,B){
  const v=[V_(bx(r*.9,r*.9),0,r*.3,B.p2,{mat:'concrete'})];
  const h=r*(3.4+R()*3.4);
  v.push(V_(ngon(R,4,r*.28,.05,.9),r*.2,h,B.p3,{mat:'metal',taper:.30,face:'lattice'}));
  for(let i=1;i<=3;i++)
    v.push(V_(bx(r*(1.5-i*.30),r*.16),h*(i/4.2),h*(i/4.2)+1.6,B.p3,{mat:'metal',edge:0}));
  if(R()<.5)v.push(V_(ngon(R,12,r*.5,.03,.92),h*.86,h*.9,B.p2,{mat:'metal',taper:.5}));  /* dish */
  v.push(V_(ngon(R,6,r*.12,.1,.9),h,h+r*.2,'rust',{lum:.45,edge:0,emit:r*1.5}));
  return v;
}
function bSolar(R,r,B){
  const v=[];
  for(const s of[-1,1])v.push(V_(bx(r*.14,r*.14,s*r*.5,0),0,r*.5,B.p3,{mat:'metal',edge:0}));
  v.push(V_(bx(r*1.7,r*1.05),r*.5,r*.5+2.4,B.p2,{mat:'metal'}));
  const cw=r*1.7/3, cd=r*1.05/2;
  for(let i=0;i<3;i++)for(let j=0;j<2;j++)
    v.push(V_(bx(cw*.82,cd*.78,(i-1)*cw,(j-.5)*cd),r*.5+2.4,r*.5+3.2,'azure',
      {mat:'glass',lum:-.15,face:'grate',edge:0}));
  return v;
}
/* ASTEROID — rock in space. `air:1`, so it floats: detached shadow + bob. */
function bAst(R,r,B){
  const v=[],f=R();
  if(f<.16){                                   /* shattered cluster */
    for(let i=0;i<3+(R()*3|0);i++){
      const a=R()*TAU,d=r*R()*.8;
      v.push(V_(shift(ngon(R,7,r*(.28+R()*.26),.6,.88),Math.cos(a)*d,Math.sin(a)*d*.75),
        0,r*(.5+R()*.7),i&1?B.p2:B.p1,{mat:'rock',taper:.5}));
    }
    return v;
  }
  if(f<.30){                                   /* long shard, tumbling axis */
    const a=R()*TAU;
    v.push(V_(rot(ngon(R,8,r*.9,.45,.30),a),0,r*(.5+R()*.4),B.p1,{mat:'rock',taper:.42}));
    v.push(V_(rot(ngon(R,6,r*.34,.5,.4),a),r*.3,r*(.9+R()*.6),B.p2,{mat:'rock',taper:.25}));
    return v;
  }
  if(f<.42){                                   /* flat plate, edge-on slab */
    v.push(V_(ngon(R,11,r*1.15,.42,.62),0,r*(.22+R()*.18),B.p1,{mat:'rock',taper:.80}));
    v.push(V_(shift(ngon(R,6,r*.36,.5,.7),(R()-.5)*r*.8,(R()-.5)*r*.5),
      0,r*(.4+R()*.35),B.p2,{mat:'rock',taper:.55}));
    return v;
  }
  if(f<.54){                                   /* binary — two locked masses */
    for(const s of[-1,1])
      v.push(V_(shift(ngon(R,8,r*(.55+R()*.14),.55,.88),s*r*.48,(R()-.5)*r*.3),
        0,r*(.7+R()*.5),s<0?B.p1:B.p2,{mat:'rock',taper:.50}));
    return v;
  }
  v.push(V_(ngon(R,10,r,.60,.88),0,r*(1.0+R()*.7),B.p1,{mat:'rock',taper:.52}));
  if(R()<.65){const a=R()*TAU,d=r*.5;
    v.push(V_(shift(ngon(R,6,r*.42,.55,.9),Math.cos(a)*d,Math.sin(a)*d*.7),
      r*.35,r*(1.1+R()*.6),B.p2,{mat:'rock',taper:.36}));}
  for(let i=0;i<1+(R()*3|0);i++){              /* impact craters punched into the cap */
    const a=R()*TAU,d=r*R()*.5;
    v.push(V_(shift(ngon(R,8,r*(.14+R()*.16),.3,.9),Math.cos(a)*d,Math.sin(a)*d*.7),
      r*(.9+R()*.5),r*(.95+R()*.5),B.p2,{lum:-.34,taper:.7,edge:0}));
  }
  if(B.g&&R()<.22)                             /* rare ore seam */
    v.push(V_(ngon(R,6,r*.2,.4,.9),r*.9,r*1.05,B.g,{lum:.4,edge:0,emit:r*1.2}));
  return v;
}
function bCrys(R,r,B){
  const v=[];const n=2+(R()*4|0);
  for(let i=0;i<n;i++){
    const a=R()*TAU,d=r*R()*.55;
    v.push(V_(shift(ngon(R,5,r*(.16+R()*.24),.22,.9),Math.cos(a)*d,Math.sin(a)*d*.8),
      0,r*(.85+R()*1.7),i?B.p2:(B.g||B.p1),{mat:'crystal',taper:.08,lum:.16,edge:0}));
  }
  v.push(V_(ngon(R,7,r*.5,.4,.85),0,r*.16,B.p2,{mat:'rock',taper:.7}));
  if(B.g)v.push(V_(ngon(R,5,r*.1,.2,.9),r*.5,r*.6,B.g,{lum:.5,edge:0,emit:r*1.8}));
  return v;
}
/* BERG — tabular · pinnacle · fractured. Ice needs topology too, not just facets. */
function bBerg(R,r,B){
  const v=[],f=R();
  if(f<.30){                                   /* tabular: wide flat table */
    v.push(V_(ngon(R,9,r*1.25,.30,.72),0,r*(.42+R()*.28),B.p1,{mat:'ice',taper:.86,lum:.12}));
    v.push(V_(shift(ngon(R,6,r*.44,.4,.8),(R()-.5)*r*.9,(R()-.5)*r*.5),
      r*.4,r*(.62+R()*.3),'ink-2',{mat:'ice',taper:.7,lum:.10}));
  }else if(f<.58){                             /* pinnacle: spires off one mass */
    v.push(V_(ngon(R,7,r*.86,.44,.84),0,r*(.36+R()*.26),B.p1,{mat:'ice',taper:.70,lum:.10}));
    for(let i=0;i<2+(R()*3|0);i++){
      const a=R()*TAU,d=r*R()*.5;
      v.push(V_(shift(ngon(R,5,r*(.20+R()*.16),.35,.86),Math.cos(a)*d,Math.sin(a)*d*.75),
        r*.3,r*(1.0+R()*1.1),'ink-2',{mat:'ice',taper:.18,lum:.14}));
    }
  }else if(f<.78){                             /* fractured: calved pair with a lead */
    for(const s of[-1,1])
      v.push(V_(shift(ngon(R,6,r*(.5+R()*.16),.42,.86),s*r*.52,(R()-.5)*r*.3),
        0,r*(.5+R()*.5),s<0?B.p1:'ink-2',{mat:'ice',taper:.60,lum:.12}));
  }else{                                       /* classic dome + peak */
    v.push(V_(ngon(R,7,r,.48,.86),0,r*(.5+R()*.5),B.p1,{mat:'ice',taper:.62,lum:.10}));
    v.push(V_(shift(ngon(R,5,r*.5,.5,.9),(R()-.5)*r*.5,(R()-.5)*r*.4),
      r*(.3+R()*.2),r*(.9+R()*.7),'ink-2',{mat:'ice',taper:.40,lum:.08}));
  }
  for(let i=0;i<2+(R()*3|0);i++){              /* brash ice at the waterline */
    const a=R()*TAU,d=r*(.8+R()*.5);
    v.push(V_(shift(ngon(R,5,r*(.10+R()*.14),.5,.8),Math.cos(a)*d,Math.sin(a)*d*.75),
      0,r*(.06+R()*.12),B.p1,{mat:'ice',taper:.6,lum:.14}));
  }
  return v;
}
/* WRECK — capital-ship hulk. Ribs, superstructure, torn plates, burning breach. */
function bWreck(R,r,B){
  const v=[];const a=R()*PI;
  const L=r*(1.4+R()*.9), Wd=r*(.30+R()*.24), h=r*(.26+R()*.30);
  const brk=R()<.45;                           /* snapped in two? */
  v.push(V_(rot(bx(brk?L*.62:L,Wd),a),0,h,B.p1,{mat:'hull',face:'plate',wear:.7}));
  if(brk){
    const aa=a+(R()-.5)*.9, off=L*.72;
    v.push(V_(shift(rot(bx(L*.44,Wd*.86),aa),Math.cos(a)*off,Math.sin(a)*off*.8),
      0,h*.8,B.p1,{mat:'hull',face:'plate',wear:.9}));
  }
  v.push(V_(rot(bx(L*.32,Wd*1.5),a),h,h+r*(.20+R()*.34),B.p2,{mat:'metal',face:'rivet'}));
  for(let i=0;i<3+(R()*4|0);i++){              /* exposed frame ribs */
    const tp=(i/6-.4)*L;
    v.push(V_(shift(rot(bx(r*.05,Wd*1.25),a),Math.cos(a)*tp,Math.sin(a)*tp*.85),
      0,h*(1.0+R()*.7),B.p3,{mat:'metal',edge:0}));
  }
  for(let i=0;i<2+(R()*4|0);i++){              /* torn plates thrown clear */
    const ang=R()*TAU,d=r*(.6+R()*.8);
    v.push(V_(shift(rot(bx(r*(.2+R()*.3),r*.12),R()*PI),Math.cos(ang)*d,Math.sin(ang)*d*.75),
      0,r*(.06+R()*.18),B.p2,{mat:'hull',taper:.85}));
  }
  const bo=(R()-.5)*L*.4;                      /* burning breach */
  v.push(V_(shift(ngon(R,6,r*.16,.4,.9),bo,0),h*.4,h*1.1,'rust',
    {lum:.45,taper:.5,edge:0,emit:r*.9}));
  if(R()<.4){const eo=-L*.42;                  /* dead engine bell */
    v.push(V_(shift(rot(ngon(R,9,Wd*.5,.06,.9),a),Math.cos(a)*eo,Math.sin(a)*eo*.85),
      0,h*.9,B.p2,{mat:'metal',taper:1.4,face:'corrug'}));}
  return v;
}
function bScrap(R,r,B){                       /* six forms — topology, not jitter */
  const v=[],f=R();
  if(f<.20){                                  /* crossed hull plates */
    for(let i=0;i<2;i++)v.push(V_(rot(bx(r*(1.2+R()*.8),r*.28),R()*PI),
      0,r*(.14+R()*.22),B.p2,{mat:'hull',face:'plate'}));
  }else if(f<.40){                            /* pressure tank */
    v.push(V_(ngon(R,10,r*.62,.08,.9),0,r*(.8+R()*.7),B.p1,{mat:'metal',taper:.72,face:'bands'}));
    v.push(V_(ngon(R,8,r*.26,.1,.9),r*.75,r*(1.1+R()*.4),B.p3,{mat:'metal',taper:.5,edge:0}));
  }else if(f<.60){                            /* snapped girder */
    const a=R()*PI;
    v.push(V_(rot(bx(r*2.2,r*.2),a),0,r*.18,B.p3,{mat:'metal',face:'lattice'}));
    v.push(V_(rot(bx(r*.9,r*.18),a+1.1),r*.16,r*(.5+R()*.6),B.p3,{mat:'metal',taper:.6}));
  }else if(f<.76){                            /* engine bell — taper > 1 flares out */
    v.push(V_(ngon(R,9,r*.62,.1,.9),0,r*(.9+R()*.6),B.p2,{mat:'metal',taper:1.55,face:'corrug'}));
    v.push(V_(ngon(R,7,r*.30,.1,.9),0,r*.2,'rust',{lum:.35,edge:0,emit:r*.9}));
  }else if(f<.90){                            /* radiator fin stack */
    const a=R()*PI;
    for(let i=0;i<3;i++)v.push(V_(shift(rot(bx(r*1.4,r*.08),a),
      Math.cos(a+PI/2)*(i-1)*r*.26,Math.sin(a+PI/2)*(i-1)*r*.2),
      0,r*(.5+R()*.4),B.p3,{mat:'metal',face:'grate'}));
  }else{                                      /* sheared cockpit / pod */
    v.push(V_(ngon(R,7,r*.5,.16,.86),0,r*(.6+R()*.4),B.p1,{mat:'hull',taper:.55}));
    v.push(V_(ngon(R,6,r*.26,.12,.86),r*.5,r*(.75+R()*.3),'azure',
      {mat:'glass',lum:.2,edge:0,emit:r*.7}));
  }
  return v;
}
/* --- flat species (decal pass, baked into the ground chunk) --- */
function bCrater(R,r,B){
  const v=[V_(ngon(R,12,r,.14,.9),0,0,B.p2,{a:.55,lum:-.3})];
  v.push(V_(ngon(R,11,r*.7,.16,.9),0,0,'bg',{a:.7,edge:0}));
  for(let i=0;i<8;i++){const a=R()*TAU,d=r*(1.0+R()*.5);
    v.push(V_(shift(rot(bx(r*.34,r*.09),a),Math.cos(a)*d,Math.sin(a)*d*.85),0,0,B.p2,{a:.3,edge:0}));}
  return v;
}
function bRubble(R,r,B){
  const v=[];
  for(let i=0;i<4+(R()*4|0);i++){const a=R()*TAU,d=r*R();
    v.push(V_(shift(rot(bx(r*(.4+R()*.5),r*(.3+R()*.4)),R()*PI),Math.cos(a)*d,Math.sin(a)*d*.8),
      0,0,B.p2,{a:.75}));}
  return v;
}
function bScorch(R,r,B){
  const v=[];
  for(let i=0;i<3;i++)v.push(V_(shift(ngon(R,10,r*(1-i*.24),.3,.85),(R()-.5)*r*.3,(R()-.5)*r*.2),
    0,0,'bg',{a:.22,edge:0}));
  return v;
}
function bHazard(R,r,B){
  const v=[];const n=5;
  for(let i=0;i<n;i++)v.push(V_(shift(rot(bx(r*.42,r*1.9),.7),(i-(n-1)/2)*r*.62,0),0,0,
    i&1?'bg':'amber',{a:.5,edge:0}));
  return v;
}
function bChevron(R,r,B){
  const v=[];
  for(let i=0;i<3;i++)v.push(V_([[-r,(i-1)*r*.55-r*.2],[0,(i-1)*r*.55+r*.28],[r,(i-1)*r*.55-r*.2],
    [r,(i-1)*r*.55],[0,(i-1)*r*.55+r*.48],[-r,(i-1)*r*.55]],0,0,'amber',{a:.42,edge:0}));
  return v;
}
/* ==========================================================================
   2.5 · SCENE PROPS — set-pieces emitted by scene rules, never by scatter.
   ========================================================================== */
/* POND — flat water: deep centre + shore rim + light glint. col:0 → walkable. */
function bPond(R,r,B){
  const v=[V_(ngon(R,14,r,.05,.9),0,0,B.p2,{a:.72,lum:-.35})];
  v.push(V_(ngon(R,12,r*.72,.06,.9),0,0,B.p2,{a:.6,lum:-.55}));
  v.push(V_(ngon(R,16,r*1.06,.18,.9),0,0,B.p2,{a:.16}));
  if(R()<.5)v.push(V_(shift(rot(bx(r*.7,r*.06),R()*PI),(R()-.5)*r*.3,(R()-.5)*r*.3),0,0,'azure',{a:.30}));
  return v;
}
/* CAMPFIRE — stone ring + crossed logs + ember glow. Small, warm, focal. */
function bCampfire(R,r,B){
  const v=[];
  for(let i=0;i<6;i++){const a=i/6*TAU+R()*.5;
    v.push(V_(shift(ngon(R,5,r*.16,.4,.85),Math.cos(a)*r*.62,Math.sin(a)*r*.62),
     0,r*(.14+R()*.10),B.p2,{mat:'rock',taper:.75}));}
  for(let i=0;i<3;i++)v.push(V_(rot(bx(r*.9,r*.13),i/3*PI+R()*.2),0,r*.10,B.p3,{mat:'bark',taper:.9,face:'corrug'}));
  v.push(V_(ngon(R,6,r*.16,.3,.9),0,0,'rust',{lum:.4,edge:0,emit:r*2.8}));
  return v;
}
/* STONE RING — standing circle, fallen lintel, core glow. Focal, tall. */
function bStoneRing(R,r,B){
  const v=[],n=7+((R()*3)|0);
  for(let i=0;i<n;i++){const a=i/n*TAU+(R()-.5)*.16, rr=r*.82*(1+(R()-.5)*.1);
    v.push(V_(shift(ngon(R,5,r*.09+R()*.06,.25,.85),Math.cos(a)*rr,Math.sin(a)*rr*.92),
     0,r*(1.0+R()*.7),B.p1,{mat:'rock',taper:.42,face:'strata'}));}
  for(let i=0;i<2;i++){const a=R()*TAU,rr=r*.82;
    v.push(V_(shift(rot(bx(r*(.5+R()*.3),r*.13),R()*PI),Math.cos(a)*rr,Math.sin(a)*rr*.92),
     0,r*.10,B.p2,{mat:'rock',taper:.8}));}
  if(B.g)v.push(V_(ngon(R,5,r*.06,.2,.9),0,r*.28,B.g,{lum:.5,edge:0,emit:r*1.4}));
  return v;
}
/* BUSH — low foliage clump. col tiny → reads as undergrowth, not a wall. */
function bBush(R,r,B){
  const v=[],n=3+((R()*3)|0);
  for(let i=0;i<n;i++){const a=R()*TAU,d=Math.pow(R(),.6)*r*.5,rr=r*(.4+R()*.35);
    v.push(V_(shift(ngon(R,8,rr,.2,.88),Math.cos(a)*d,Math.sin(a)*d*.8),
      r*.05,r*(.22+R()*.22),i&1?B.p2:B.p1,{mat:'foliage',taper:.7,lum:.02,edge:0}));}
  return v;
}
/* LOG — fallen trunk with moss caps. */
function bLog(R,r,B){
  const v=[],a=R()*PI,sl=r*(1.6+R()*.9);
  v.push(V_(rot(bx(sl,r*.16),a),0,r*.16,B.p3,{mat:'bark',taper:.92,face:'corrug',lum:-.05}));
  for(let i=0;i<2+(R()*2|0);i++){const t=(R()-.5)*sl*.7;
    v.push(V_(shift(rot(ngon(R,7,r*.14,.3,.85),a),Math.cos(a)*t,Math.sin(a)*t*.9),
      r*.14,r*(.2+R()*.12),B.p2,{mat:'foliage',taper:.8,edge:0}));}
  return v;
}

/* ==========================================================================
   3 · SPECIES TABLE — a species is a data row, not a renderer.
     cell/dens/r   scatter grid, occupancy, size range   (ENVIRONMENT-PIPELINE §6)
     vari          how many variants get baked
     region        macro-noise mask -> groves, rockfields, districts
     clus          satellites per hit  -> clumping instead of confetti
     col           collision radius factor (0 = walk through)
     air           floats: live ground shadow + altitude offset
     flat          decal pass -> baked into the ground chunk, free per frame
   ========================================================================== */
const SPECIES={};
function S_(id,build,o){o.id=id;o.build=build;SPECIES[id]=o;return o;}

S_('rock',    bRock, {cell:132,dens:.40,r:[9,24],  vari:32,salt:11,region:'rocky', clus:2,col:.55});
S_('boulder', bRock, {cell:300,dens:.34,r:[26,52], vari:22,salt:12,region:'rocky', col:.6});
S_('mesa',    bMesa, {cell:620,dens:.42,r:[46,118],vari:14,salt:13,region:'rocky', col:.55});
S_('tree',    bTree, {cell:96, dens:.70,r:[13,28], vari:32,salt:14,region:'forest',clus:3,col:.22});
S_('pine',    bPine, {cell:120,dens:.55,r:[15,30], vari:24,salt:15,region:'forest',clus:2,col:.22});
S_('dead',    bDead, {cell:220,dens:.35,r:[12,26], vari:12,salt:16,col:.2});
S_('bldg',    bBldg, {cell:200,dens:.58,r:[20,52], vari:28,salt:17,region:'urban', col:.58});
S_('ruin',    bRuin, {cell:175,dens:.45,r:[14,38], vari:24,salt:18,col:.5});
S_('crate',   bCrate,{cell:150,dens:.30,r:[8,18],  vari:12,salt:19,clus:2,col:.55});
S_('pylon',   bPylon,{cell:380,dens:.40,r:[9,16],  vari:10,salt:20,col:.4});
S_('solar',   bSolar,{cell:330,dens:.30,r:[16,30], vari:10,salt:21,col:.5});
S_('asteroid',bAst,  {cell:175,dens:.52,r:[11,38], vari:32,salt:22,clus:2,col:.55,air:1});
S_('crystal', bCrys, {cell:210,dens:.32,r:[12,30], vari:14,salt:23,col:.4});
S_('berg',    bBerg, {cell:230,dens:.42,r:[16,42], vari:20,salt:24,col:.55});
S_('wreck',   bWreck,{cell:400,dens:.56,r:[42,132],vari:14,salt:25,col:.5});
S_('scrap',   bScrap,{cell:130,dens:.38,r:[7,20],  vari:26,salt:26,clus:2,col:0,air:1});
S_('contain', bCrate,{cell:190,dens:.46,r:[17,34], vari:14,salt:32,clus:2,col:.55});
S_('gantry',  bPylon,{cell:290,dens:.44,r:[17,29], vari:12,salt:33,col:.45});
S_('floe',    bBerg, {cell:150,dens:.40,r:[8,19],  vari:16,salt:34,clus:2,col:.4});
S_('crater',  bCrater,{cell:260,dens:.40,r:[16,40],vari:8, salt:27,flat:1});
S_('rubble',  bRubble,{cell:118,dens:.45,r:[5,13], vari:8, salt:28,flat:1});
S_('scorch',  bScorch,{cell:300,dens:.30,r:[18,44],vari:6, salt:29,flat:1});
S_('hazard',  bHazard,{cell:420,dens:.35,r:[14,22],vari:5, salt:30,flat:1});
S_('chevron', bChevron,{cell:360,dens:.30,r:[12,20],vari:5,salt:31,flat:1});
/* scene props — cell is deliberately huge: these exist ONLY because a scene
   rule puts them there. A scatter pass can never meet them. */
S_('pond',     bPond,     {cell:900,dens:.02,r:[70,120],vari:6, salt:41,flat:1,noDress:1});
S_('campfire', bCampfire, {cell:900,dens:.02,r:[6,9],   vari:6, salt:42,col:.5});
S_('stonering',bStoneRing,{cell:900,dens:.02,r:[14,24], vari:8, salt:43,col:.35});
S_('bush',     bBush,     {cell:900,dens:.02,r:[7,12],  vari:12,salt:44,col:.12});
S_('log',      bLog,      {cell:900,dens:.02,r:[8,13],  vari:8, salt:45,col:.5});

const GROUND={
 plate(g,X,Y,S,B){
   const b=col(B.gtone);
   g.fillStyle=shade(b,-.80);g.fillRect(X-2,Y-2,S+4,S+4);
   const q=64;
   for(let i=Math.floor(X/q);i*q<X+S;i++)for(let j=Math.floor(Y/q);j*q<Y+S;j++){
     const v=hsh(i*1.7,j*2.3);
     g.fillStyle=shade(b,v>.5?-.76:-.82);
     g.fillRect(i*q,j*q,q,q);
     if(v>.9){g.fillStyle=shade(b,-.70);g.fillRect(i*q+7,j*q+7,q-14,q-14);}
   }
   g.strokeStyle=shade(b,-.66);g.lineWidth=1*LW;g.beginPath();
   for(let i=Math.floor(X/q);i*q<=X+S;i++){g.moveTo(i*q,Y-2);g.lineTo(i*q,Y+S+2);}
   for(let j=Math.floor(Y/q);j*q<=Y+S;j++){g.moveTo(X-2,j*q);g.lineTo(X+S+2,j*q);}
   g.stroke();
   g.fillStyle=shade(b,-.52);
   for(let i=Math.floor(X/q);i*q<X+S;i++)for(let j=Math.floor(Y/q);j*q<Y+S;j++)
     for(const d of[[6,6],[q-6,6],[6,q-6],[q-6,q-6]])g.fillRect(i*q+d[0]-1,j*q+d[1]-1,2,2);
 },
 blocks(g,X,Y,S,B){
   const b=col(B.gtone);
   g.fillStyle=shade(b,-.86);g.fillRect(X-2,Y-2,S+4,S+4);
   const q=170,m=26;
   for(let i=Math.floor(X/q)-1;i*q<X+S;i++)for(let j=Math.floor(Y/q)-1;j*q<Y+S;j++){
     const v=hsh(i*3.1,j*4.7);
     g.fillStyle=shade(b,v>.5?-.78:-.83);
     g.fillRect(i*q+m,j*q+m,q-m*2,q-m*2);
     g.strokeStyle=shade(b,-.70);g.lineWidth=1*LW;
     g.strokeRect(i*q+m,j*q+m,q-m*2,q-m*2);
     if(v>.8){g.fillStyle=shade(b,-.74);g.fillRect(i*q+m+10,j*q+m+10,q-m*2-20,q-m*2-20);}
   }
   g.strokeStyle=shade(b,-.58);g.lineWidth=1*LW;g.setLineDash([12,15]);g.beginPath();
   for(let i=Math.floor(X/q)-1;i*q<=X+S;i++){g.moveTo(i*q,Y-2);g.lineTo(i*q,Y+S+2);}
   for(let j=Math.floor(Y/q)-1;j*q<=Y+S;j++){g.moveTo(X-2,j*q);g.lineTo(X+S+2,j*q);}
   g.stroke();g.setLineDash([]);
 },
 soil(g,X,Y,S,B){
   const b=col(B.gtone);
   g.fillStyle=shade(b,-.80);g.fillRect(X-2,Y-2,S+4,S+4);
   const q=34;
   g.globalAlpha=.5;
   for(let i=Math.floor(X/q)-1;i*q<X+S+q;i++)for(let j=Math.floor(Y/q)-1;j*q<Y+S+q;j++){
     const n=vnoise(i*.30,j*.30,3.3);
     g.fillStyle=shade(b,n>.55?-.68:n>.42?-.76:-.85);
     const x=i*q+hsh(i,j)*q,y=j*q+hsh(j,i)*q,r=11+hsh(i+3,j)*21;
     g.beginPath();g.ellipse(x,y,r,r*.8,0,0,TAU);g.fill();
   }
   g.globalAlpha=1;g.fillStyle=shade(b,-.58);
   for(let i=Math.floor(X/q);i*q<X+S;i++)for(let j=Math.floor(Y/q);j*q<Y+S;j++)
     if(hsh(i*5.1,j*7.3)>.55)g.fillRect(i*q+hsh(i+9,j)*q,j*q+hsh(j+9,i)*q,2.5,1.6);
 },
 dune(g,X,Y,S,B){
   const b=col(B.gtone), band=26;
   g.fillStyle=shade(b,-.84);g.fillRect(X-2,Y-2,S+4,S+4);
   const ridge=(x,j)=>j*band+Math.sin(x*.011+j*1.7)*7+Math.sin(x*.0042+j*.6)*12;
   for(let j=Math.floor(Y/band)-2;j*band<Y+S+band;j++){
     g.fillStyle=shade(b,(j&1)?-.76:-.83);
     g.beginPath();
     for(let x=X-8;x<=X+S+8;x+=8)x===X-8?g.moveTo(x,ridge(x,j)):g.lineTo(x,ridge(x,j));
     for(let x=X+S+8;x>=X-8;x-=8)g.lineTo(x,ridge(x,j+1));
     g.closePath();g.fill();
     g.strokeStyle=shade(b,-.62);g.lineWidth=1*LW;g.beginPath();
     for(let x=X-8;x<=X+S+8;x+=8)x===X-8?g.moveTo(x,ridge(x,j)):g.lineTo(x,ridge(x,j));
     g.stroke();
   }
 },
 ice(g,X,Y,S,B){
   const b=col(B.gtone), q=46;
   g.fillStyle=shade(b,-.82);g.fillRect(X-2,Y-2,S+4,S+4);
   for(let i=Math.floor(X/q)-1;i*q<X+S+q;i++)for(let j=Math.floor(Y/q)-1;j*q<Y+S+q;j++){
     const n=vnoise(i*.26,j*.26,6.1);
     g.fillStyle=shade(b,n>.56?-.62:n>.43?-.72:-.80);
     const x=i*q+hsh(i,j)*q*.5,y=j*q+hsh(j,i)*q*.5,r=q*(.5+hsh(i+2,j)*.55);
     g.beginPath();g.ellipse(x,y,r,r*.85,0,0,TAU);g.fill();
   }
   g.strokeStyle=shade(b,-.48);g.lineWidth=1*LW;
   for(let i=Math.floor(X/q)-1;i*q<X+S+q;i++)for(let j=Math.floor(Y/q)-1;j*q<Y+S+q;j++){
     if(hsh(i*2.9,j*1.3)<.62)continue;
     let x=i*q,y=j*q;g.beginPath();g.moveTo(x,y);
     for(let k=0;k<4;k++){const a=hsh(i+k,j-k)*TAU;x+=Math.cos(a)*q*.55;y+=Math.sin(a)*q*.55;g.lineTo(x,y);}
     g.stroke();
   }
 },
 ash(g,X,Y,S,B){
   const b=col(B.gtone), q=40;
   g.fillStyle=shade(b,-.88);g.fillRect(X-2,Y-2,S+4,S+4);
   g.globalAlpha=.55;
   for(let i=Math.floor(X/q)-1;i*q<X+S+q;i++)for(let j=Math.floor(Y/q)-1;j*q<Y+S+q;j++){
     const n=vnoise(i*.33,j*.33,9.9);
     g.fillStyle=shade(b,n>.5?-.78:-.88);
     g.beginPath();g.ellipse(i*q+hsh(i,j)*q,j*q+hsh(j,i)*q,q*.6,q*.5,0,0,TAU);g.fill();
   }
   g.globalAlpha=1;
 }
};

/* ---------- ground chunk cache (§7). key = biome : chunkX , chunkY ---------- */
const CH=256, chunks=new Map();
let chunkBudget=0, chunkGen=0, chunkHit=0;
function getChunk(cx,cy){
  const k=BIO.id+':'+cx+','+cy;
  let e=chunks.get(k);
  if(e){chunkHit++;chunks.delete(k);chunks.set(k,e);return e;}
  if(chunkBudget<=0)return null;
  chunkBudget--;chunkGen++;
  e=buildChunk(cx,cy);chunks.set(k,e);
  if(chunks.size>380)chunks.delete(chunks.keys().next().value);
  return e;
}
function buildChunk(cx,cy){
  const X=cx*CH, Y=cy*CH;
  const c=document.createElement('canvas');
  c.width=Math.round(CH*BS);c.height=Math.round(CH*BS);
  const g=c.getContext('2d');
  g.setTransform(BS,0,0,BS,0,0);
  g.translate(-X,-Y);
  g.beginPath();g.rect(X,Y,CH,CH);g.clip();
  if(typeof paintWorldChunk==='function')paintWorldChunk(g,X,Y,CH);
  else GROUND[BIO.ground](g,X,Y,CH,BIO);
  for(const sid of BIO.props){                     /* decal pass, cached forever */
    const spec=SPECIES[sid];if(!spec.flat)continue;
    scatterRect(spec,X-70,Y-70,X+CH+70,Y+CH+70,(x,y,r,vi)=>{
      g.save();g.translate(x,y);
      const vols=variantVols(spec,vi,r);
      for(let i=0;i<vols.length;i++)drawFlat(g,vols[i],1);
      g.restore();
    });
  }
  drawPaths(g,X,Y,CH);
  return{c:c,X:X,Y:Y};
}
/* TRAILS ARE GROUND. Paths bake into the chunk canvas — cached, free per
   frame, and they READ as terrain rather than as a row of paint. Stroked in
   three passes (packed earth / worn centre / edge ruts + deterministic
   speckle) so a trail has body, not a single flat band. */
function segHitsBox(x0,y0,x1,y1,minX,minY,maxX,maxY){
  let t0=0,t1=1,dx=x1-x0,dy=y1-y0;
  const clip=(p,q)=>{
    if(p===0)return q>=0;
    const r=q/p;
    if(p<0){if(r>t1)return false;if(r>t0)t0=r;}
    else{if(r<t0)return false;if(r<t1)t1=r;}
    return true;
  };
  return clip(-dx,x0-minX)&&clip(dx,maxX-x0)&&clip(-dy,y0-minY)&&clip(dy,maxY-y0);
}
function drawPaths(g,X,Y,S){
  if(!PATH.length)return;
  const b=col(BIO.gtone);
  g.save();g.lineCap='round';
  for(const p of PATH){
    const pad=p.w, minX=X-pad, minY=Y-pad, maxX=X+S+pad, maxY=Y+S+pad;
    if(!segHitsBox(p.x0,p.y0,p.x1,p.y1,minX,minY,maxX,maxY))continue;
    const a=Math.atan2(p.y1-p.y0,p.x1-p.x0);
    g.strokeStyle=shade(b,-.55);g.lineWidth=p.w;
    g.beginPath();g.moveTo(p.x0,p.y0);g.lineTo(p.x1,p.y1);g.stroke();
    g.strokeStyle=shade(b,-.68);g.lineWidth=p.w*.48;
    g.beginPath();g.moveTo(p.x0,p.y0);g.lineTo(p.x1,p.y1);g.stroke();
    g.globalAlpha=.5;g.strokeStyle=shade(b,-.38);g.lineWidth=Math.max(1.5,p.w*.07);
    g.beginPath();g.moveTo(p.x0,p.y0);g.lineTo(p.x1,p.y1);g.stroke();
    g.globalAlpha=1;
    const L=Math.hypot(p.x1-p.x0,p.y1-p.y0), n=(L/9)|0;
    g.fillStyle=shade(b,-.72);
    for(let k=0;k<n;k++){
      const t=(k+hsh(k*3.7,((p.x0*13.7+p.y0*7.1)|0))*.4)/n;
      const px2=p.x0+(p.x1-p.x0)*t, py2=p.y0+(p.y1-p.y0)*t;
      if(px2<minX||px2>maxX||py2<minY||py2>maxY)continue;
      g.fillRect(px2-Math.cos(a)*1.4,py2-Math.sin(a)*1.4,2.4,1.8);
    }
  }
  g.restore();
}
function groundPass(cx,pcy,t){
  const x0=cx-W/2-4, x1=cx+W/2+4;
  const wy0=(pcy-H/2)/t-4, wy1=(pcy+H/2)/t+4;
  const i0=Math.floor(x0/CH),i1=Math.floor(x1/CH);
  const j0=Math.floor(wy0/CH),j1=Math.floor(wy1/CH);
  for(let i=i0;i<=i1;i++)for(let j=j0;j<=j1;j++){
    const e=getChunk(i,j);
    if(e)ctx.drawImage(e.c,e.X,e.Y*t,CH,CH*t);
    else{ctx.fillStyle=BIO.base;ctx.fillRect(i*CH,j*CH*t,CH,CH*t);}
  }
}
/* void ground is parallax — cannot be chunked, so it draws live in screen space */
function stars(cx,pcy){
  const LY=[[.14,1,.34],[.34,1.4,.55],[.68,1.9,.85]];
  for(let L=0;L<3;L++){
    const par=LY[L][0],sz=LY[L][1],al=LY[L][2],q=96;
    const ox=cx*par,oy=pcy*par;
    const i0=Math.floor(ox/q)-1,i1=Math.floor((ox+W)/q)+1;
    const j0=Math.floor(oy/q)-1,j1=Math.floor((oy+H)/q)+1;
    for(let i=i0;i<=i1;i++)for(let j=j0;j<=j1;j++){
      const h=hsh(i*1.7+L*31,j*2.3+L*17);
      if(h<.44)continue;
      ctx.globalAlpha=al*(.45+hsh(i+5,j+L)*.55);
      ctx.fillStyle=h>.987?C.azure:h>.972?C.rust:C.ink;
      ctx.fillRect(i*q+hsh(i,j+L)*q-ox, j*q+hsh(j,i+L)*q-oy, sz,sz);
    }
  }
  ctx.globalAlpha=1;
}
/* tactical grid overlay (pass 3) */
function gridPass(cx,pcy,t){
  const G=BIO.grid;if(!G)return;
  const S=G.step;
  ctx.save();ctx.globalAlpha=G.a;ctx.strokeStyle=C[G.c]||G.c;ctx.lineWidth=1*LW;
  const x0=cx-W/2-S,x1=cx+W/2+S,wy0=(pcy-H/2)/t-S,wy1=(pcy+H/2)/t+S;
  ctx.beginPath();
  if(G.hex){
    for(let j=Math.floor(wy0/S);j*S<=wy1;j++){
      const off=(j&1)?S/2:0;
      for(let i=Math.floor((x0-off)/S);i*S+off<=x1;i++){
        const x=i*S+off,y=j*S*t;
        for(let k=0;k<6;k++){const a=k/6*TAU+PI/6;
          const px2=x+Math.cos(a)*S*.5, py2=y+Math.sin(a)*S*.5*t;
          k?ctx.lineTo(px2,py2):ctx.moveTo(px2,py2);}
        ctx.closePath();
      }
    }
  }else{
    for(let i=Math.floor(x0/S);i*S<=x1;i++){ctx.moveTo(i*S,wy0*t);ctx.lineTo(i*S,wy1*t);}
    for(let j=Math.floor(wy0/S);j*S<=wy1;j++){ctx.moveTo(x0,j*S*t);ctx.lineTo(x1,j*S*t);}
  }
  ctx.stroke();ctx.restore();
}

