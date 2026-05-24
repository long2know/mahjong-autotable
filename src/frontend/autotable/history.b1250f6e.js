import{_ as E}from"./admin-panel-core.c48ae4f3.js";import{k as g,C as v,y as b,j as w}from"./autotable-src.104c1af5.js";import"./signalr.2eaee90b.js";const S=20,o={installed:!1,open:!1,range:"30",customFrom:null,customTo:null,format:"json",rows:[],loading:!1,error:null,endpointAvailable:null,sort:"date",sortAsc:!1};function h(t){return document.getElementById(t)}function k(){o.installed||(L(),R(),o.installed=!0)}function C(){o.installed||k();const t=h("history-modal");t!==null&&(o.open=!0,t.classList.add("history-modal-open"),t.setAttribute("aria-hidden","false"),v(t),p(),window.setTimeout(()=>{const e=h("history-modal-close");e==null||e.focus()},50))}function f(){const t=h("history-modal");t!==null&&(o.open=!1,t.classList.remove("history-modal-open"),t.setAttribute("aria-hidden","true"),g(t))}function L(){var n;const t=(n=document.querySelector("#profile-recent-games"))==null?void 0:n.closest(".profile-page-section");if(t===null||t.querySelector("#profile-history-link")!==null)return;const e=t.querySelector(".profile-page-section-title"),s=document.createElement("button");if(s.id="profile-history-link",s.type="button",s.className="btn btn-info btn-sm profile-history-link",s.setAttribute("data-testid","profile-history-link"),s.textContent="📥 Match history",s.setAttribute("aria-label","Open match history export modal"),s.addEventListener("click",()=>C()),e!==null&&e.parentElement!==null){const r=document.createElement("div");r.className="profile-page-section-title-row",e.parentElement.insertBefore(r,e),r.appendChild(e),r.appendChild(s)}else t.insertBefore(s,t.firstChild)}function R(){if(h("history-modal")!==null)return;const t=document.createElement("div");t.id="history-modal",t.className="history-modal",t.setAttribute("role","dialog"),t.setAttribute("aria-modal","true"),t.setAttribute("aria-label","Match history export"),t.setAttribute("aria-hidden","true"),t.setAttribute("data-testid","history-modal"),t.hidden=!0,t.innerHTML=`
    <div class="history-modal-backdrop" data-history-dismiss></div>
    <div class="history-modal-shell" role="document">
      <header class="history-modal-header">
        <h2 class="history-modal-title">📥 Match history</h2>
        <button id="history-modal-close" type="button"
                class="history-modal-close"
                data-testid="history-modal-close"
                aria-label="Close match history">×</button>
      </header>
      <section class="history-modal-controls">
        <label class="history-modal-field">
          <span class="history-modal-field-label">Date range</span>
          <select id="history-date-range"
                  class="form-control form-control-sm"
                  data-testid="history-date-range">
            <option value="7">Last 7 days</option>
            <option value="30" selected>Last 30 days</option>
            <option value="90">Last 90 days</option>
            <option value="365">Last 365 days</option>
            <option value="custom">Custom range…</option>
          </select>
        </label>
        <div id="history-custom-range" class="history-modal-custom-range" hidden>
          <label class="history-modal-field">
            <span class="history-modal-field-label">From</span>
            <input id="history-date-from" type="date"
                   class="form-control form-control-sm"
                   data-testid="history-date-from">
          </label>
          <label class="history-modal-field">
            <span class="history-modal-field-label">To</span>
            <input id="history-date-to" type="date"
                   class="form-control form-control-sm"
                   data-testid="history-date-to">
          </label>
        </div>
        <fieldset class="history-modal-format" data-testid="history-format-toggle">
          <legend class="history-modal-field-label">Format</legend>
          <label class="history-modal-format-option">
            <input type="radio" name="history-format" value="json"
                   data-testid="history-format-json" checked>
            <span>JSON</span>
          </label>
          <label class="history-modal-format-option">
            <input type="radio" name="history-format" value="csv"
                   data-testid="history-format-csv">
            <span>CSV</span>
          </label>
        </fieldset>
        <button id="history-download" type="button"
                class="btn btn-success btn-sm history-modal-download"
                data-testid="history-download">
          ⬇ Download
        </button>
      </section>
      <section class="history-modal-status">
        <div id="history-modal-error"
             class="history-modal-error" hidden aria-live="polite"></div>
        <div id="history-modal-loading"
             class="history-modal-loading" hidden aria-live="polite">
          Loading recent matches…
        </div>
        <div id="history-modal-unavailable"
             class="history-modal-unavailable" hidden>
          Match-history export is not yet available on this server.
        </div>
      </section>
      <section class="history-modal-recent">
        <h3 class="history-modal-section-title">Recent matches</h3>
        <div id="history-recent-table-host"
             class="history-modal-recent-host"></div>
      </section>
    </div>
  `,document.body.appendChild(t),t.querySelectorAll("[data-history-dismiss]").forEach(a=>{a.addEventListener("click",()=>f())});const e=t.querySelector("#history-modal-close");e==null||e.addEventListener("click",()=>f());const s=t.querySelector("#history-date-range");s==null||s.addEventListener("change",()=>{o.range=s.value;const a=t.querySelector("#history-custom-range");a!==null&&b(a,o.range!=="custom"),p()});const n=t.querySelector("#history-date-from");n==null||n.addEventListener("change",()=>{o.customFrom=n.value===""?null:n.value,o.range==="custom"&&p()});const r=t.querySelector("#history-date-to");r==null||r.addEventListener("change",()=>{o.customTo=r.value===""?null:r.value,o.range==="custom"&&p()}),t.querySelectorAll('input[name="history-format"]').forEach(a=>{a.addEventListener("change",()=>{a.checked&&(a.value==="json"||a.value==="csv")&&(o.format=a.value)})});const c=t.querySelector("#history-download");c==null||c.addEventListener("click",()=>void M()),document.addEventListener("keydown",a=>{o.open&&a.key==="Escape"&&f()})}function I(){if(o.range==="custom")return{from:o.customFrom,to:o.customTo};const t=parseInt(o.range,10);if(isNaN(t)||t<=0)return{from:null,to:null};const e=new Date;return{from:new Date(e.getTime()-t*24*60*60*1e3).toISOString(),to:e.toISOString()}}function A(t){var a;const e=w(),s=(a=e==null?void 0:e.playerId)!=null?a:"";if(s===""||s==="offline")return null;const n=new URLSearchParams;n.set("playerId",s),n.set("format",t);const{from:r,to:c}=I();return r!==null&&r!==""&&n.set("from",r),c!==null&&c!==""&&n.set("to",c),`/api/games?${n.toString()}`}async function p(){if(!o.open)return;const t=A("json");if(t===null){o.rows=[],o.error="Sign in to view your match history.",m();return}o.loading=!0,o.error=null,m();try{const e=await fetch(t,{credentials:"same-origin",headers:{Accept:"application/json"}});if(e.status===404){o.endpointAvailable=!1,o.rows=[],o.loading=!1,m();return}if(!e.ok)throw new Error(`HTTP ${e.status}`);o.endpointAvailable=!0;const s=await e.json();o.rows=N(s).slice(0,S)}catch(e){o.error=e.message,o.rows=[]}finally{o.loading=!1,m()}}function N(t){const e=[],s=Array.isArray(t)?t:t!==null&&typeof t=="object"&&Array.isArray(t.games)?t.games:[];for(const n of s){if(n===null||typeof n!="object")continue;const r=n,c=typeof r.gameId=="string"&&r.gameId!==""?r.gameId:typeof r.id=="string"?r.id:"";if(c==="")continue;const a=typeof r.finishedAt=="string"?r.finishedAt:typeof r.completedAt=="string"?r.completedAt:null,l=(()=>{const d=typeof r.result=="string"?r.result.toLowerCase():"";return d==="win"||d==="won"?"win":d==="loss"||d==="lost"?"loss":d==="draw"||d==="washout"?"draw":"unknown"})(),i=typeof r.finalScore=="number"?r.finalScore:typeof r.score=="number"?r.score:0,u=typeof r.opponentSummary=="string"?r.opponentSummary:typeof r.summary=="string"?r.summary:typeof r.opponents=="string"?r.opponents:"";e.push({gameId:c,finishedAt:a,result:l,finalScore:i,opponentSummary:u})}return e}function m(){const t=h("history-recent-table-host"),e=h("history-modal-error"),s=h("history-modal-loading"),n=h("history-modal-unavailable");if(t!==null&&(e!==null&&(o.error!==null?(v(e),e.textContent=o.error):(g(e),e.textContent="")),s!==null&&b(s,!o.loading),n!==null&&b(n,o.endpointAvailable!==!1),t.replaceChildren(),o.endpointAvailable!==!1)){if(o.rows.length===0&&!o.loading){const r=document.createElement("div");r.className="history-modal-empty",r.textContent="No matches found in this range.",t.appendChild(r);return}t.appendChild(T())}}function T(){const t=document.createElement("table");t.className="history-modal-table history-recent-table",t.setAttribute("data-testid","history-recent-table"),t.setAttribute("role","table");const e=document.createElement("thead"),s=document.createElement("tr"),n=[{key:"date",label:"Date"},{key:null,label:"Opponents"},{key:"result",label:"Result"},{key:"score",label:"Score"},{key:null,label:""}];for(const a of n){const l=document.createElement("th");if(l.className="history-modal-th",l.scope="col",l.textContent=a.label,a.key!==null){l.setAttribute("tabindex","0"),l.setAttribute("role","columnheader"),a.key===o.sort&&(l.classList.add("history-modal-th-active"),l.setAttribute("aria-sort",o.sortAsc?"ascending":"descending"));const i=()=>{o.sort===a.key?o.sortAsc=!o.sortAsc:(o.sort=a.key,o.sortAsc=a.key!=="date"),m()};l.addEventListener("click",i),l.addEventListener("keydown",u=>{(u.key==="Enter"||u.key===" ")&&(u.preventDefault(),i())})}s.appendChild(l)}e.appendChild(s),t.appendChild(e);const r=document.createElement("tbody");return D(o.rows).forEach((a,l)=>{const i=document.createElement("tr");i.className=`history-modal-row history-modal-row-${a.result}`,i.setAttribute("data-testid",`history-recent-row-${l}`),i.setAttribute("data-game-id",a.gameId),y(i,x(a.finishedAt),"history-modal-cell-date"),y(i,a.opponentSummary||"—","history-modal-cell-opponents"),y(i,q(a.result),`history-modal-cell-result history-modal-cell-result-${a.result}`),y(i,$(a.finalScore),"history-modal-cell-score");const u=document.createElement("td");u.className="history-modal-cell-actions";const d=document.createElement("button");d.type="button",d.className="btn btn-sm btn-info history-modal-replay",d.textContent="🎞",d.title="Watch replay",d.setAttribute("aria-label",`Watch replay for game ${a.gameId.slice(0,8)}`),d.addEventListener("click",()=>{j(a.gameId)}),u.appendChild(d),i.appendChild(u),r.appendChild(i)}),t.appendChild(r),t}async function j(t){if(t!=="")try{const e=await E(()=>import("./replay-launcher.689f03f3.js"),[],import.meta.url);f(),e.openReplayForGame(t)}catch(e){}}function D(t){const e=t.slice(),s=o.sortAsc?1:-1;return e.sort((n,r)=>{switch(o.sort){case"date":{const c=n.finishedAt===null?0:Date.parse(n.finishedAt),a=r.finishedAt===null?0:Date.parse(r.finishedAt);return s*(c-a)}case"result":return s*n.result.localeCompare(r.result);case"score":return s*(n.finalScore-r.finalScore);default:return 0}}),e}function y(t,e,s){const n=document.createElement("td");n.className=`history-modal-cell ${s}`,n.textContent=e,t.appendChild(n)}function x(t){if(t===null||t==="")return"—";const e=Date.parse(t);return isNaN(e)?t:new Date(e).toLocaleDateString()}function $(t){if(!isFinite(t))return"—";const e=Math.round(t);return e>0?`+${e}`:String(e)}function q(t){return t.length===0?t:t.charAt(0).toUpperCase()+t.slice(1)}async function M(){const t=A(o.format);if(t===null){o.error="Sign in to download your match history.",m();return}o.error=null,m();try{const e=await fetch(t,{credentials:"same-origin",headers:{Accept:o.format==="csv"?"text/csv":"application/json"}});if(e.status===404){o.endpointAvailable=!1,m();return}if(!e.ok)throw new Error(`HTTP ${e.status}`);o.endpointAvailable=!0;const s=await e.blob(),n=window.URL.createObjectURL(s),r=document.createElement("a");r.href=n,r.download=_(o.format),document.body.appendChild(r),r.click(),r.remove(),window.setTimeout(()=>window.URL.revokeObjectURL(n),5e3)}catch(e){o.error=e.message,m()}}function _(t){return`mahjong-history-${new Date().toISOString().slice(0,10)}.${t}`}export{f as closeHistoryModal,k as installHistoryModal,C as openHistoryModal};
