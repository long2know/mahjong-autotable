const b="bracket-listing-overlay";function p(e){if(typeof e=="string")return e;if(e!==null&&typeof e=="object"){const t=e;if(typeof t.displayName=="string")return t.displayName;if(typeof t.name=="string")return t.name}return""}function m(e,t){return{id:typeof e.id=="string"&&e.id!==""?e.id:`match-${t}`,roundNumber:typeof e.roundNumber=="number"&&Number.isFinite(e.roundNumber)?Math.max(1,Math.floor(e.roundNumber)):1,matchIndex:typeof e.matchIndex=="number"&&Number.isFinite(e.matchIndex)?Math.max(0,Math.floor(e.matchIndex)):t,seedA:typeof e.seedA=="number"&&Number.isFinite(e.seedA)?e.seedA:null,seedB:typeof e.seedB=="number"&&Number.isFinite(e.seedB)?e.seedB:null,playerA:p(e.playerA),playerB:p(e.playerB),winnerSeed:typeof e.winnerSeed=="number"&&Number.isFinite(e.winnerSeed)?e.winnerSeed:null,status:typeof e.status=="string"?e.status:"pending",bracketSide:typeof e.bracketSide=="string"?e.bracketSide:"winners"}}function y(e){let t=null;if(Array.isArray(e))t=e;else if(e!==null&&typeof e=="object"){const s=e;Array.isArray(s.brackets)?t=s.brackets:Array.isArray(s.records)&&(t=s.records)}return t===null?[]:t.filter(s=>s!==null&&typeof s=="object").map((s,r)=>m(s,r))}function n(e){return e.replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;").replace(/'/g,"&#39;")}function h(e){const t=e.toLowerCase();return t==="completed"||t==="complete"?"bracket-listing-status-completed":t==="in-progress"||t==="in_progress"||t==="inprogress"?"bracket-listing-status-active":"bracket-listing-status-pending"}function f(){let e=document.getElementById(b);return e!==null?(e.innerHTML="",e):(e=document.createElement("div"),e.id=b,e.className="bracket-listing-overlay",e.setAttribute("role","dialog"),e.setAttribute("aria-modal","false"),e.setAttribute("data-testid","bracket-listing-overlay"),document.body.appendChild(e),e)}function v(){const e=document.getElementById(b);e!==null&&e.parentNode!==null&&e.parentNode.removeChild(e)}function d(e,t){return`
    <div class="bracket-listing-card" data-testid="bracket-listing-empty">
      <header class="bracket-listing-header">
        <h2 class="bracket-listing-title">Tournament brackets</h2>
        <button type="button" class="bracket-listing-close"
                data-testid="bracket-listing-close"
                aria-label="Close brackets overlay">×</button>
      </header>
      <p class="bracket-listing-tournament-id">Tournament <code>${n(e)}</code></p>
      <p class="bracket-listing-empty-message">${n(t)}</p>
    </div>`}function A(e){const t=e.winnerSeed!==null&&e.winnerSeed===e.seedA,s=e.winnerSeed!==null&&e.winnerSeed===e.seedB,r=t?"bracket-listing-player bracket-listing-player-winner":"bracket-listing-player",o=s?"bracket-listing-player bracket-listing-player-winner":"bracket-listing-player",l=e.playerA!==""?e.playerA:e.seedA!==null?`Seed ${e.seedA}`:"TBD",a=e.playerB!==""?e.playerB:e.seedB!==null?`Seed ${e.seedB}`:"TBD",i=h(e.status),u=e.bracketSide!==""?` data-bracket-side="${n(e.bracketSide)}"`:"";return`
    <article class="bracket-listing-match" data-testid="bracket-listing-match"
             data-match-id="${n(e.id)}"${u}>
      <div class="${r}" data-testid="bracket-listing-player-a">
        <span class="bracket-listing-seed">${e.seedA!==null?n(String(e.seedA)):"—"}</span>
        <span class="bracket-listing-player-name">${n(l)}</span>
        ${t?'<span class="bracket-listing-winner-badge" aria-label="Winner">★</span>':""}
      </div>
      <div class="${o}" data-testid="bracket-listing-player-b">
        <span class="bracket-listing-seed">${e.seedB!==null?n(String(e.seedB)):"—"}</span>
        <span class="bracket-listing-player-name">${n(a)}</span>
        ${s?'<span class="bracket-listing-winner-badge" aria-label="Winner">★</span>':""}
      </div>
      <span class="bracket-listing-status ${i}"
            data-testid="bracket-listing-status">${n(e.status)}</span>
    </article>`}function $(e,t){var l;if(t.length===0)return d(e,"No brackets have been generated for this tournament yet.");const s=new Map;for(const a of t){const i=(l=s.get(a.roundNumber))!=null?l:[];i.push(a),s.set(a.roundNumber,i)}const o=Array.from(s.keys()).sort((a,i)=>a-i).map(a=>{var u;const i=((u=s.get(a))!=null?u:[]).slice().sort((g,k)=>g.matchIndex-k.matchIndex);return`
      <section class="bracket-listing-round"
               data-testid="bracket-listing-round-${a}"
               data-round-number="${a}">
        <h3 class="bracket-listing-round-title">Round ${a}</h3>
        <div class="bracket-listing-matches">
          ${i.map(A).join("")}
        </div>
      </section>`}).join("");return`
    <div class="bracket-listing-card" data-testid="bracket-listing-card">
      <header class="bracket-listing-header">
        <h2 class="bracket-listing-title">Tournament brackets</h2>
        <button type="button" class="bracket-listing-close"
                data-testid="bracket-listing-close"
                aria-label="Close brackets overlay">×</button>
      </header>
      <p class="bracket-listing-tournament-id">Tournament <code>${n(e)}</code></p>
      <div class="bracket-listing-grid" data-testid="bracket-listing-grid">
        ${o}
      </div>
    </div>`}function c(e){const t=e.querySelector(".bracket-listing-close");t==null||t.addEventListener("click",()=>{v();try{const s=new URL(window.location.href);s.pathname="/",s.search="",window.history.replaceState(window.history.state,"",s.pathname+s.search+s.hash)}catch(s){}})}async function B(e){const t=f();t.innerHTML=`
    <div class="bracket-listing-card" data-testid="bracket-listing-loading">
      <header class="bracket-listing-header">
        <h2 class="bracket-listing-title">Tournament brackets</h2>
        <button type="button" class="bracket-listing-close"
                data-testid="bracket-listing-close"
                aria-label="Close brackets overlay">×</button>
      </header>
      <p class="bracket-listing-loading-message">Loading brackets…</p>
    </div>`,c(t);let s;try{s=await fetch(`/api/tournaments/${encodeURIComponent(e)}/brackets`,{credentials:"same-origin",headers:{Accept:"application/json"}})}catch(l){t.innerHTML=d(e,"Could not reach the brackets service."),c(t);return}if(s.status===404){t.innerHTML=d(e,"No brackets found for this tournament."),c(t);return}if(!s.ok){t.innerHTML=d(e,"Brackets unavailable."),c(t);return}let r;try{r=await s.json()}catch(l){t.innerHTML=d(e,"Brackets response malformed."),c(t);return}const o=y(r);t.innerHTML=$(e,o),c(t)}export{B as openBracketListing};
