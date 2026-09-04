const LIGHT={x:-.68,y:.73};      /* from the south-west; front-left faces catch it */
const rgba=(c,a)=>'rgba('+c[0]+','+c[1]+','+c[2]+','+a+')';

/* MATERIAL — what a surface is made of. Drives cap texture, face texture, the
   chamfer lip and how hard the top rim catches light. Every one of these runs at
   BAKE time, so the entire detail budget is free at runtime (PERF-BIBLE §8). */
const MAT={
  rock:    {cap:'mottle', face:'strata', bev:.10, rim:.30},
  metal:   {cap:'panel',  face:'rivet',  bev:.16, rim:.46},
  hull:    {cap:'plate',  face:'plate',  bev:.12, rim:.40},
  concrete:{cap:'seam',   face:'bands',  bev:.10, rim:.28},
  glass:   {cap:'panel',  face:'win',    bev:.14, rim:.50},
  ice:     {cap:'crack',  face:'facet',  bev:.13, rim:.62},
  crystal: {cap:null,     face:'facet',  bev:.26, rim:.70},
  foliage: {cap:'clump',  face:null,     bev:0,   rim:.18},
  bark:    {cap:null,     face:'corrug', bev:0,   rim:.20},
  sand:    {cap:'ripple', face:'strata', bev:.06, rim:.22},
  none:    {cap:null,     face:null,     bev:0,   rim:.26}
};

function V_(p,z0,z1,c,o){
  o=o||{};
  return{p:p,z0:z0,z1:z1,rgb:col(c),taper:o.taper==null?1:o.taper,lum:o.lum||0,
         mat:MAT[o.mat]||MAT.none,face:o.face,lit:o.lit,litD:o.litD==null?.28:o.litD,
         wear:o.wear||0,emit:o.emit||0,edge:o.edge!==0,a:o.a};
}
function maxY(p){let m=-1e9;for(const q of p)if(q[1]>m)m=q[1];return m;}

function drawVol(g,V,t){
  const p=V.p,n=p.length,k=V.taper,M=V.mat;
  let cx=0,cy=0;for(let i=0;i<n;i++){cx+=p[i][0];cy+=p[i][1];}cx/=n;cy/=n;
  const B=new Array(n),T=new Array(n);
  for(let i=0;i<n;i++){
    B[i]=[p[i][0], p[i][1]*t - V.z0];
    T[i]=[cx+(p[i][0]-cx)*k, (cy+(p[i][1]-cy)*k)*t - V.z1];
  }
  const base=V.rgb;
  const hl=clamp(V.z1/300,0,.26)+V.lum;          /* height -> luminance (channel 3) */
  const eg=shade(base,-.60);
  const sd=Math.abs(cx*13.7+cy*7.3+V.z1*3.13)+1; /* stable per-volume texture seed */
  /* HEADROOM BOOST: the darker the material, the harder detail has to lean on
     light, because there is no room left below it. Without this, anything built
     from --panel reads as a flat silhouette no matter how much texture is drawn. */
  const bl=(base[0]*.3+base[1]*.59+base[2]*.11)/255;
  const bo=clamp(.36-bl,0,.36);
  g.lineWidth=1*LW;
  /* --- side skirt: only camera-facing edges; the rest is hidden by the cap --- */
  for(let i=0;i<n;i++){
    const j=(i+1)%n;
    const dx=p[j][0]-p[i][0], dy=p[j][1]-p[i][1];
    let nx=dy, ny=-dx; const L=Math.hypot(nx,ny)||1; nx/=L; ny/=L;
    const mx=(p[i][0]+p[j][0])*.5-cx, my=(p[i][1]+p[j][1])*.5-cy;
    if(nx*mx+ny*my<0){nx=-nx;ny=-ny;}
    if(ny<=.02)continue;
    const lam=Math.max(0,nx*LIGHT.x+ny*LIGHT.y);
    const tt=-.40+.40*lam+hl*.45;
    g.beginPath();
    g.moveTo(B[i][0],B[i][1]);g.lineTo(B[j][0],B[j][1]);
    g.lineTo(T[j][0],T[j][1]);g.lineTo(T[i][0],T[i][1]);g.closePath();
    /* vertical gradient: contact-dark at the base, lifting toward the cap.
       This is the cheapest single upgrade to the volume read. */
    const gy0=(B[i][1]+B[j][1])*.5, gy1=(T[i][1]+T[j][1])*.5;
    if(Math.abs(gy1-gy0)>2){
      const gr=g.createLinearGradient(0,gy0,0,gy1);
      gr.addColorStop(0,shade(base,tt-.24));
      gr.addColorStop(.55,shade(base,tt+.02+bo*.5));
      gr.addColorStop(1,shade(base,tt+.22+bo));
      g.fillStyle=gr;
    }else g.fillStyle=shade(base,tt+bo*.4);
    g.fill();
    faceTex(g,V,M,B[i],B[j],T[i],T[j],base,tt,sd+i*3.7,bo);
    if(V.edge){g.strokeStyle=eg;g.stroke();}
  }
  /* --- cap --- */
  if(k>.02){
    g.beginPath();g.moveTo(T[0][0],T[0][1]);
    for(let i=1;i<n;i++)g.lineTo(T[i][0],T[i][1]);
    g.closePath();
    g.fillStyle=shade(base,.15+hl+bo*.5);g.fill();
    capTex(g,V,M,T,base,hl,sd,bo);
    /* chamfer lip: an inset ring reads as a bevelled edge, not a cut-out */
    if(M.bev>0){
      const ccy=cy*t-V.z1;
      g.beginPath();
      for(let i=0;i<n;i++){
        const x=cx+(T[i][0]-cx)*(1-M.bev), y=ccy+(T[i][1]-ccy)*(1-M.bev);
        i?g.lineTo(x,y):g.moveTo(x,y);
      }
      g.closePath();g.fillStyle=shade(base,.15+hl+.11+bo*.6);g.fill();
    }
    /* rim light: the BACK edges of the cap catch the sky, front edges keep the
       dark seam. This is what separates one object from the one behind it. */
    if(V.edge){
      for(let i=0;i<n;i++){
        const j=(i+1)%n;
        const dx=p[j][0]-p[i][0], dy=p[j][1]-p[i][1];
        let nx=dy,ny=-dx;const L2=Math.hypot(nx,ny)||1;nx/=L2;ny/=L2;
        const mx=(p[i][0]+p[j][0])*.5-cx,my=(p[i][1]+p[j][1])*.5-cy;
        if(nx*mx+ny*my<0){nx=-nx;ny=-ny;}
        g.strokeStyle=ny<=.02?shade(base,.18+hl+M.rim+bo):eg;
        g.beginPath();g.moveTo(T[i][0],T[i][1]);g.lineTo(T[j][0],T[j][1]);g.stroke();
      }
    }
  }
  if(V.emit){                                    /* baked additive glow */
    const x=cx,y=cy*t-V.z1,r=V.emit;
    g.save();g.globalCompositeOperation='lighter';
    const gr=g.createRadialGradient(x,y,0,x,y,r);
    gr.addColorStop(0,rgba(base,.55));gr.addColorStop(.4,rgba(base,.20));
    gr.addColorStop(1,rgba(base,0));
    g.fillStyle=gr;g.fillRect(x-r,y-r,r*2,r*2);
    g.restore();
  }
}
/* SIDE-FACE TEXTURE — ten surface vocabularies. Bake time only, free at runtime. */
function faceTex(g,V,M,b0,b1,t0,t1,base,tt,sd,bo){
  const kind=V.face!==undefined?V.face:M.face;
  if(!kind&&!V.wear)return;
  const x0=Math.min(b0[0],b1[0],t0[0],t1[0]), x1=Math.max(b0[0],b1[0],t0[0],t1[0]);
  const y0=Math.min(t0[1],t1[1]), y1=Math.max(b0[1],b1[1]);
  const w=x1-x0,h=y1-y0;
  if(w<3.5||h<4)return;
  g.save();g.clip();
  /* CONTRAST RULE: a dark surface has no headroom downward — mixing 26% toward
     --bg on a #1b212a panel is a 5-level delta, i.e. invisible. So detail is
     carried mostly by LIGHT, biased up from the face's own level. Still only
     ever mixes toward --ink / --bg, so D6 and the 17-colour limit hold. */
  const dk=shade(base,tt-.30), md=shade(base,tt+.20+bo*.7), lt=shade(base,tt+.46+bo*1.5);
  if(kind==='bands'){
    g.fillStyle=dk;for(let y=y0+4;y<y1-2;y+=7)g.fillRect(x0,y,w,2);
  }else if(kind==='seam'){
    g.fillStyle=dk;for(let x=x0+5;x<x1;x+=11)g.fillRect(x,y0,1,h);
  }else if(kind==='rivet'){                     /* panelled plate + rivet rows */
    g.fillStyle=dk;
    for(let x=x0+7;x<x1;x+=13)g.fillRect(x,y0,1,h);
    for(let y=y0+9;y<y1;y+=15)g.fillRect(x0,y,w,1);
    g.fillStyle=lt;
    for(let x=x0+7;x<x1;x+=13)for(let y=y0+5;y<y1-2;y+=7.5)g.fillRect(x-1,y,1.4,1.4);
  }else if(kind==='plate'){                     /* irregular welded hull plating */
    for(let y=y0+2;y<y1;y+=5+(sd*3)%4){
      g.fillStyle=hsh(sd,y*.31)>.5?md:dk;
      g.fillRect(x0+hsh(sd+1,y)*5,y,w,1);
    }
    g.fillStyle=dk;
    for(let x=x0+9;x<x1;x+=17)g.fillRect(x,y0,1,h);
  }else if(kind==='strata'){                    /* sediment layering — rock, mesa */
    g.globalAlpha=.75;
    for(let y=y0+2;y<y1;y+=3.4){
      g.fillStyle=hsh(sd,y*.7)>.5?md:dk;
      g.beginPath();g.moveTo(x0,y);
      for(let x=x0;x<=x1;x+=6)g.lineTo(x,y+Math.sin(x*.22+sd)*1.5);
      g.lineTo(x1,y+2.3);g.lineTo(x0,y+2.3);g.closePath();g.fill();
    }
    g.globalAlpha=1;
  }else if(kind==='corrug'){                    /* corrugation / bark */
    g.globalAlpha=.62;
    for(let x=x0;x<x1;x+=3.2){g.fillStyle=((x/3.2)|0)&1?dk:lt;g.fillRect(x,y0,1.6,h);}
    g.globalAlpha=1;
  }else if(kind==='lattice'){                   /* girder X-bracing */
    g.strokeStyle=dk;g.lineWidth=1.5*LW;g.beginPath();
    for(let x=x0-h;x<x1+h;x+=9){g.moveTo(x,y1);g.lineTo(x+h,y0);g.moveTo(x,y0);g.lineTo(x+h,y1);}
    g.stroke();g.lineWidth=1*LW;
  }else if(kind==='grate'){
    g.fillStyle=dk;
    for(let x=x0;x<x1;x+=3)g.fillRect(x,y0,1,h);
    for(let y=y0;y<y1;y+=3)g.fillRect(x0,y,w,1);
  }else if(kind==='facet'){                     /* crystalline facets + fracture lines */
    g.globalAlpha=.72;g.fillStyle=lt;
    g.beginPath();g.moveTo(x0,y1);g.lineTo(x0+w*.42,y0);g.lineTo(x0+w*.64,y1);g.closePath();g.fill();
    g.globalAlpha=.44;g.fillStyle=md;
    g.beginPath();g.moveTo(x0+w*.58,y1);g.lineTo(x0+w*.88,y0);g.lineTo(x1,y1);g.closePath();g.fill();
    g.globalAlpha=.66;g.strokeStyle=lt;g.lineWidth=1*LW;g.beginPath();
    for(let i=0;i<3;i++){
      const xa=x0+hsh(sd+i,2.1)*w;
      g.moveTo(xa,y0);g.lineTo(xa+(hsh(sd+i,3.3)-.5)*w*.55,y1);
    }
    g.stroke();g.globalAlpha=1;
  }else if(kind==='win'){                       /* window grid with lit cells */
    const lit=V.lit?col(V.lit):null, d2=V.litD;
    for(let y=y0+5;y<y1-4;y+=6.5)
      for(let x=x0+3;x<x1-3.4;x+=6){
        const hv=hsh(x*.41+V.z1*.13,y*.57+sd);
        g.fillStyle=(lit&&hv>1-d2)?shade(lit,.12):dk;
        g.fillRect(x,y,3.4,3);
      }
  }
  if(V.wear>0){                                 /* stain runs + blast holes */
    g.globalAlpha=.26*V.wear;g.fillStyle=shade(base,-.55);
    for(let i=0;i<5;i++)
      g.fillRect(x0+hsh(sd+i,3.3)*w,y0,1+hsh(sd+i,7.7)*2.6,h*(.3+hsh(sd+i,9.1)*.7));
    g.globalAlpha=.55*V.wear;g.fillStyle=shade(base,-.92);
    for(let i=0;i<3;i++){
      if(hsh(sd+i,15.5)>.55)continue;
      g.beginPath();g.ellipse(x0+hsh(sd+i,11.1)*w,y0+hsh(sd+i,13.3)*h,
        w*.10+2,h*.06+2,0,0,TAU);g.fill();
    }
    g.globalAlpha=1;
  }
  g.restore();
}
/* CAP TEXTURE — the top face is the part this camera shows most. */
function capTex(g,V,M,T,base,hl,sd,bo){
  const kind=M.cap;if(!kind)return;
  let x0=1e9,y0=1e9,x1=-1e9,y1=-1e9;
  for(let i=0;i<T.length;i++){const q=T[i];
    if(q[0]<x0)x0=q[0];if(q[0]>x1)x1=q[0];if(q[1]<y0)y0=q[1];if(q[1]>y1)y1=q[1];}
  const w=x1-x0,h=y1-y0;
  if(w<5||h<3)return;
  g.save();g.clip();
  const t0=.15+hl;
  const dk=shade(base,t0-.30), md=shade(base,t0+.16+bo*.6), lt=shade(base,t0+.38+bo);
  if(kind==='mottle'){
    g.globalAlpha=.70;
    for(let i=0;i<7;i++){
      g.fillStyle=hsh(sd+i,1.7)>.5?dk:lt;
      const rx=w*(.12+hsh(sd+i,2.3)*.26);
      g.beginPath();g.ellipse(x0+hsh(sd+i,3.1)*w,y0+hsh(sd+i,4.7)*h,rx,rx*.6,0,0,TAU);g.fill();
    }
    g.globalAlpha=1;
  }else if(kind==='panel'){
    g.fillStyle=dk;
    for(let x=x0+w*.28;x<x1;x+=Math.max(5,w*.30))g.fillRect(x,y0,1,h);
    for(let y=y0+h*.3;y<y1;y+=Math.max(4,h*.34))g.fillRect(x0,y,w,1);
    g.fillStyle=md;g.fillRect(x0+w*.10,y0+h*.16,w*.22,h*.28);
  }else if(kind==='plate'){
    g.fillStyle=dk;
    for(let i=0;i<4;i++)g.fillRect(x0,y0+h*(i+.5)/4.5,w,1);
    g.globalAlpha=.45;g.fillStyle=lt;g.fillRect(x0,y0,w*.4,h);g.globalAlpha=1;
  }else if(kind==='seam'){
    g.fillStyle=dk;
    for(let x=x0;x<x1;x+=Math.max(6,w*.25))g.fillRect(x,y0,1,h);
  }else if(kind==='crack'){
    g.globalAlpha=.5;                           /* floe mottle under the fractures */
    for(let i=0;i<5;i++){
      g.fillStyle=hsh(sd+i,1.9)>.5?md:dk;
      const rx=w*(.14+hsh(sd+i,2.7)*.24);
      g.beginPath();g.ellipse(x0+hsh(sd+i,3.5)*w,y0+hsh(sd+i,4.9)*h,rx,rx*.62,0,0,TAU);g.fill();
    }
    g.globalAlpha=.85;g.strokeStyle=lt;g.lineWidth=1.3*LW;
    for(let i=0;i<3;i++){
      let x=x0+hsh(sd+i,5.5)*w,y=y0+hsh(sd+i,6.6)*h;
      g.beginPath();g.moveTo(x,y);
      for(let s=0;s<4;s++){const a=hsh(sd+i+s,7.7)*TAU;
        x+=Math.cos(a)*w*.25;y+=Math.sin(a)*h*.3;g.lineTo(x,y);}
      g.stroke();
    }
    g.globalAlpha=1;
  }else if(kind==='clump'){                     /* foliage reads as clumps, not a disc */
    g.globalAlpha=.80;
    for(let i=0;i<9;i++){
      const hv=hsh(sd+i,8.8);
      g.fillStyle=hv>.62?lt:hv>.3?md:dk;
      const rx=w*(.13+hsh(sd+i,9.9)*.18);
      g.beginPath();g.ellipse(x0+hsh(sd+i,10.1)*w,y0+hsh(sd+i,11.3)*h,rx,rx*.72,0,0,TAU);g.fill();
    }
    g.globalAlpha=1;
  }else if(kind==='ripple'){
    g.globalAlpha=.62;g.strokeStyle=dk;g.lineWidth=1*LW;
    for(let y=y0;y<y1;y+=3.5){
      g.beginPath();
      for(let x=x0;x<=x1;x+=5)x===x0?g.moveTo(x,y):g.lineTo(x,y+Math.sin(x*.3+sd)*1.2);
      g.stroke();
    }
    g.globalAlpha=1;
  }
  g.restore();
}
function drawFlat(g,V,t){
  const p=V.p;
  g.globalAlpha=V.a==null?1:V.a;
  g.fillStyle=shade(V.rgb,V.lum||0);
  g.beginPath();g.moveTo(p[0][0],p[0][1]*t);
  for(let i=1;i<p.length;i++)g.lineTo(p[i][0],p[i][1]*t);
  g.closePath();g.fill();
  if(V.edge){g.strokeStyle=shade(V.rgb,-.5);g.lineWidth=1*LW;g.stroke();}
  g.globalAlpha=1;
}

/* ---------- footprint generators ---------- */
function ngon(R,n,r,jit,sq){
  const p=new Array(n);
  for(let i=0;i<n;i++){
    const a=(i+(R()-.5)*jit*.6)/n*TAU;
    const rr=r*(1-jit*.45+R()*jit*.9);
    p[i]=[Math.cos(a)*rr,Math.sin(a)*rr*(sq==null?1:sq)];
  }
  return p;
}
function bx(w,d,ox,oy){ox=ox||0;oy=oy||0;
  return[[ox-w/2,oy-d/2],[ox+w/2,oy-d/2],[ox+w/2,oy+d/2],[ox-w/2,oy+d/2]];}
function shift(p,dx,dy){const o=new Array(p.length);
  for(let i=0;i<p.length;i++)o[i]=[p[i][0]+dx,p[i][1]+dy];return o;}
function rot(p,a){const c=Math.cos(a),s=Math.sin(a),o=new Array(p.length);
  for(let i=0;i<p.length;i++)o[i]=[p[i][0]*c-p[i][1]*s,p[i][0]*s+p[i][1]*c];return o;}
/* ==========================================================================
   7 · BAKE — volume[] -> sprite. One drawImage per instance at draw time.
   Keyed on tilt bucket because an extrusion is only valid at the tilt it was
   built for. A per-frame bake budget means a tilt sweep never spikes: misses
   fall back to path-draw for that frame only.
   ========================================================================== */
const sprites=new Map();
let bakeBudget=0, bakeN=0, blits=0, paths=0;
function getSprite(spec,vi,tq,stage){
  const k=spec.id+':'+BIO.id+':'+vi+':'+(stage||0)+':'+tq;
  let s=sprites.get(k);
  if(s)return s;
  if(bakeBudget<=0)return null;
  bakeBudget--;bakeN++;
  s=bakeSprite(spec,vi,tq,stage);sprites.set(k,s);
  if(sprites.size>1100)sprites.delete(sprites.keys().next().value);
  return s;
}
function volSort(a,b){return(a.z0*1000+maxY(a.p))-(b.z0*1000+maxY(b.p));}
function bakeSprite(spec,vi,tq,stage){
  const t=tq/20, r=variantR(spec,vi);
  const vols=variantVols(spec,vi,r,stage||0);
  let x0=1e9,y0=1e9,x1=-1e9,y1=-1e9,fr=0;
  for(const V of vols){
    const k=V.taper;let cx=0,cy=0;
    for(const q of V.p){cx+=q[0];cy+=q[1];}cx/=V.p.length;cy/=V.p.length;
    for(const q of V.p){
      const bxx=q[0],byy=q[1]*t-V.z0;
      const txx=cx+(q[0]-cx)*k, tyy=(cy+(q[1]-cy)*k)*t-V.z1;
      if(bxx<x0)x0=bxx;if(txx<x0)x0=txx;
      if(bxx>x1)x1=bxx;if(txx>x1)x1=txx;
      if(byy<y0)y0=byy;if(tyy<y0)y0=tyy;
      if(byy>y1)y1=byy;if(tyy>y1)y1=tyy;
      fr=Math.max(fr,Math.hypot(q[0],q[1]));
    }
    if(V.emit){                       /* the baked glow spills past the geometry */
      const ex=cx, ey=cy*t-V.z1, e=V.emit;
      if(ex-e<x0)x0=ex-e; if(ex+e>x1)x1=ex+e;
      if(ey-e<y0)y0=ey-e; if(ey+e>y1)y1=ey+e;
    }
  }
  if(!spec.air){                     /* union the contact shadow into the bbox */
    const sr=fr*1.03+2;
    x0=Math.min(x0,2-sr);x1=Math.max(x1,2+sr);
    y0=Math.min(y0,2-sr*t);y1=Math.max(y1,2+sr*t);
  }
  const pad=5;
  x0=Math.floor(x0-pad);x1=Math.ceil(x1+pad);
  y0=Math.floor(y0-pad);y1=Math.ceil(y1+pad);
  const w=Math.max(2,x1-x0), h=Math.max(2,y1-y0);
  const c=document.createElement('canvas');
  c.width=Math.round(w*BS);c.height=Math.round(h*BS);
  const g=c.getContext('2d');
  g.setTransform(BS,0,0,BS,0,0);g.translate(-x0,-y0);
  if(!spec.air){                                   /* contact shadow baked in */
    g.globalAlpha=.38;g.fillStyle='#000';
    g.beginPath();g.ellipse(2,2,fr*1.02,fr*1.02*t,0,0,TAU);g.fill();
    g.globalAlpha=1;
  }
  vols.sort(volSort);
  for(let i=0;i<vols.length;i++)drawVol(g,vols[i],t);
  return{c:c,ox:x0,oy:y0,w:w,h:h,fr:fr};
}
