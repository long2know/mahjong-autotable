const o="replays-listing-overlay";function d(t){const e=typeof t.replayId=="string"&&t.replayId!==""?t.replayId:typeof t.id=="string"&&t.id!==""?t.id:null;if(e===null)return null;const l=typeof t.completedAt=="string"&&t.completedAt!==""?t.completedAt:typeof t.completedAtUtc=="string"?t.completedAtUtc:"",a=typeof t.variant=="string"?t.variant:"",n=typeof t.turnCount=="number"&&Number.isFinite(t.turnCount)?Math.max(0,Math.floor(t.turnCount)):null;return{replayId:e,completedAt:l,variant:a,turnCount:n}}function y(t){let e=null;if(Array.isArray(t))e=t;else if(t!==null&&typeof t=="object"){const a=t;Array.isArray(a.replays)?e=a.replays:Array.isArray(a.items)&&(e=a.items)}if(e===null)return[];const l=[];for(const a of e){if(a===null||typeof a!="object")continue;const n=d(a);n!==null&&l.push(n)}return l}function s(t){return t.replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;").replace(/'/g,"&#39;")}function u(t){if(t==="")return"—";const e=new Date(t);if(Number.isNaN(e.getTime()))return t;const l=e.getUTCFullYear(),a=String(e.getUTCMonth()+1).padStart(2,"0"),n=String(e.getUTCDate()).padStart(2,"0"),p=String(e.getUTCHours()).padStart(2,"0"),c=String(e.getUTCMinutes()).padStart(2,"0");return`${l}-${a}-${n} ${p}:${c} UTC`}function g(){let t=document.getElementById(o);return t!==null?(t.innerHTML="",t):(t=document.createElement("div"),t.id=o,t.className="replays-listing-overlay",t.setAttribute("role","dialog"),t.setAttribute("aria-modal","false"),t.setAttribute("data-testid","replays-listing-overlay"),document.body.appendChild(t),t)}function h(){const t=document.getElementById(o);t!==null&&t.parentNode!==null&&t.parentNode.removeChild(t)}function i(t){return`
    <div class="replays-listing-card" data-testid="replays-listing-empty">
      <header class="replays-listing-header">
        <h2 class="replays-listing-title">Recent replays</h2>
        <button type="button" class="replays-listing-close"
                data-testid="replays-listing-close"
                aria-label="Close replays overlay">×</button>
      </header>
      <p class="replays-listing-empty-message">${s(t)}</p>
    </div>`}function m(t){return`
    <div class="replays-listing-card" data-testid="replays-listing-card">
      <header class="replays-listing-header">
        <h2 class="replays-listing-title">Recent replays</h2>
        <button type="button" class="replays-listing-close"
                data-testid="replays-listing-close"
                aria-label="Close replays overlay">×</button>
      </header>
      <table class="replays-listing-table" data-testid="replays-listing-table">
        <thead>
          <tr>
            <th scope="col">Completed</th>
            <th scope="col">Variant</th>
            <th scope="col">Turns</th>
            <th scope="col">Action</th>
          </tr>
        </thead>
        <tbody>${t.map(l=>{const a=`/?action=replay&replayId=${encodeURIComponent(l.replayId)}`;return`
      <tr data-testid="replays-listing-row" data-replay-id="${s(l.replayId)}">
        <td>${s(u(l.completedAt))}</td>
        <td>${s(l.variant!==""?l.variant:"—")}</td>
        <td>${l.turnCount!==null?s(String(l.turnCount)):"—"}</td>
        <td>
          <a class="replays-listing-link" href="${s(a)}"
             data-testid="replays-listing-open">Open replay</a>
        </td>
      </tr>`}).join("")}</tbody>
      </table>
    </div>`}function r(t){const e=t.querySelector(".replays-listing-close");e==null||e.addEventListener("click",()=>{h();try{const l=new URL(window.location.href);l.pathname="/",l.search="",window.history.replaceState(window.history.state,"",l.pathname+l.search+l.hash)}catch(l){}})}async function f(){const t=g();t.innerHTML=`
    <div class="replays-listing-card" data-testid="replays-listing-loading">
      <header class="replays-listing-header">
        <h2 class="replays-listing-title">Recent replays</h2>
        <button type="button" class="replays-listing-close"
                data-testid="replays-listing-close"
                aria-label="Close replays overlay">×</button>
      </header>
      <p class="replays-listing-loading-message">Loading replays…</p>
    </div>`,r(t);let e;try{e=await fetch("/api/replays",{credentials:"same-origin",headers:{Accept:"application/json"}})}catch(n){t.innerHTML=i("Could not reach the replays service."),r(t);return}if(e.status===404){t.innerHTML=i("No replays found."),r(t);return}if(!e.ok){t.innerHTML=i("Replays unavailable."),r(t);return}let l;try{l=await e.json()}catch(n){t.innerHTML=i("Replays response malformed."),r(t);return}const a=y(l);if(a.length===0){t.innerHTML=i("No replays yet."),r(t);return}t.innerHTML=m(a),r(t)}export{f as openReplaysListing};
