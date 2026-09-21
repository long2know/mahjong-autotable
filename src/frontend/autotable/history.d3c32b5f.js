import{_ as E}from"./admin-panel-core.e1c43197.js";import{Q as g,am as v,ag as b,I as w}from"./autotable-src.b77d769d.js";const S=20,o={installed:!1,open:!1,range:"30",customFrom:null,customTo:null,format:"json",rows:[],loading:!1,error:null,endpointAvailable:null,sort:"date",sortAsc:!1};function h(t){return document.getElementById(t)}function k(){o.installed||(L(),I(),o.installed=!0)}function C(){o.installed||k();const t=h("history-modal");t!==null&&(o.open=!0,t.classList.add("history-modal-open"),t.setAttribute("aria-hidden","false"),v(t),p(),window.setTimeout(()=>{const e=h("history-modal-close");e==null||e.focus()},50))}function f(){const t=h("history-modal");t!==null&&(o.open=!1,t.classList.remove("history-modal-open"),t.setAttribute("aria-hidden","true"),g(t))}function L(){var n;const t=(n=document.querySelector("#profile-recent-games"))==null?void 0:n.closest(".profile-page-section");if(t===null||t.querySelector("#profile-history-link")!==null)return;const e=t.querySelector(".profile-page-section-title"),a=document.createElement("button");if(a.id="profile-history-link",a.type="button",a.className="btn btn-info btn-sm profile-history-link",a.setAttribute("data-testid","profile-history-link"),a.textContent="📥 Match history",a.setAttribute("aria-label","Open match history export modal"),a.addEventListener("click",()=>C()),e!==null&&e.parentElement!==null){const s=document.createElement("div");s.className="profile-page-section-title-row",e.parentElement.insertBefore(s,e),s.appendChild(e),s.appendChild(a)}else t.insertBefore(a,t.firstChild)}function I(){if(h("history-modal")!==null)return;const t=document.createElement("div");t.id="history-modal",t.className="history-modal",t.setAttribute("role","dialog"),t.setAttribute("aria-modal","true"),t.setAttribute("aria-label","Match history export"),t.setAttribute("aria-hidden","true"),t.setAttribute("data-testid","history-modal"),t.hidden=!0,t.innerHTML=`
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
  `,document.body.appendChild(t),t.querySelectorAll("[data-history-dismiss]").forEach(r=>{r.addEventListener("click",()=>f())});const e=t.querySelector("#history-modal-close");e==null||e.addEventListener("click",()=>f());const a=t.querySelector("#history-date-range");a==null||a.addEventListener("change",()=>{o.range=a.value;const r=t.querySelector("#history-custom-range");r!==null&&b(r,o.range!=="custom"),p()});const n=t.querySelector("#history-date-from");n==null||n.addEventListener("change",()=>{o.customFrom=n.value===""?null:n.value,o.range==="custom"&&p()});const s=t.querySelector("#history-date-to");s==null||s.addEventListener("change",()=>{o.customTo=s.value===""?null:s.value,o.range==="custom"&&p()}),t.querySelectorAll('input[name="history-format"]').forEach(r=>{r.addEventListener("change",()=>{r.checked&&(r.value==="json"||r.value==="csv")&&(o.format=r.value)})});const c=t.querySelector("#history-download");c==null||c.addEventListener("click",()=>void M()),document.addEventListener("keydown",r=>{o.open&&r.key==="Escape"&&f()})}function R(){if(o.range==="custom")return{from:o.customFrom,to:o.customTo};const t=parseInt(o.range,10);if(isNaN(t)||t<=0)return{from:null,to:null};const e=new Date;return{from:new Date(e.getTime()-t*24*60*60*1e3).toISOString(),to:e.toISOString()}}function A(t){var r;const e=w(),a=(r=e==null?void 0:e.playerId)!=null?r:"";if(a===""||a==="offline")return null;const n=new URLSearchParams;n.set("playerId",a),n.set("format",t);const{from:s,to:c}=R();return s!==null&&s!==""&&n.set("from",s),c!==null&&c!==""&&n.set("to",c),`/api/games?${n.toString()}`}async function p(){if(!o.open)return;const t=A("json");if(t===null){o.rows=[],o.error="Sign in to view your match history.",m();return}o.loading=!0,o.error=null,m();try{const e=await fetch(t,{credentials:"same-origin",headers:{Accept:"application/json"}});if(e.status===404){o.endpointAvailable=!1,o.rows=[],o.loading=!1,m();return}if(!e.ok)throw new Error(`HTTP ${e.status}`);o.endpointAvailable=!0;const a=await e.json();o.rows=N(a).slice(0,S)}catch(e){o.error=e.message,o.rows=[]}finally{o.loading=!1,m()}}function N(t){const e=[],a=Array.isArray(t)?t:t!==null&&typeof t=="object"&&Array.isArray(t.games)?t.games:[];for(const n of a){if(n===null||typeof n!="object")continue;const s=n,c=typeof s.gameId=="string"&&s.gameId!==""?s.gameId:typeof s.id=="string"?s.id:"";if(c==="")continue;const r=typeof s.finishedAt=="string"?s.finishedAt:typeof s.completedAt=="string"?s.completedAt:null,l=(()=>{const d=typeof s.result=="string"?s.result.toLowerCase():"";return d==="win"||d==="won"?"win":d==="loss"||d==="lost"?"loss":d==="draw"||d==="washout"?"draw":"unknown"})(),i=typeof s.finalScore=="number"?s.finalScore:typeof s.score=="number"?s.score:0,u=typeof s.opponentSummary=="string"?s.opponentSummary:typeof s.summary=="string"?s.summary:typeof s.opponents=="string"?s.opponents:"";e.push({gameId:c,finishedAt:r,result:l,finalScore:i,opponentSummary:u})}return e}function m(){const t=h("history-recent-table-host"),e=h("history-modal-error"),a=h("history-modal-loading"),n=h("history-modal-unavailable");if(t!==null&&(e!==null&&(o.error!==null?(v(e),e.textContent=o.error):(g(e),e.textContent="")),a!==null&&b(a,!o.loading),n!==null&&b(n,o.endpointAvailable!==!1),t.replaceChildren(),o.endpointAvailable!==!1)){if(o.rows.length===0&&!o.loading){const s=document.createElement("div");s.className="history-modal-empty",s.textContent="No matches found in this range.",t.appendChild(s);return}t.appendChild(T())}}function T(){const t=document.createElement("table");t.className="history-modal-table history-recent-table",t.setAttribute("data-testid","history-recent-table"),t.setAttribute("role","table");const e=document.createElement("thead"),a=document.createElement("tr"),n=[{key:"date",label:"Date"},{key:null,label:"Opponents"},{key:"result",label:"Result"},{key:"score",label:"Score"},{key:null,label:""}];for(const r of n){const l=document.createElement("th");if(l.className="history-modal-th",l.scope="col",l.textContent=r.label,r.key!==null){l.setAttribute("tabindex","0"),l.setAttribute("role","columnheader"),r.key===o.sort&&(l.classList.add("history-modal-th-active"),l.setAttribute("aria-sort",o.sortAsc?"ascending":"descending"));const i=()=>{o.sort===r.key?o.sortAsc=!o.sortAsc:(o.sort=r.key,o.sortAsc=r.key!=="date"),m()};l.addEventListener("click",i),l.addEventListener("keydown",u=>{(u.key==="Enter"||u.key===" ")&&(u.preventDefault(),i())})}a.appendChild(l)}e.appendChild(a),t.appendChild(e);const s=document.createElement("tbody");return j(o.rows).forEach((r,l)=>{const i=document.createElement("tr");i.className=`history-modal-row history-modal-row-${r.result}`,i.setAttribute("data-testid",`history-recent-row-${l}`),i.setAttribute("data-game-id",r.gameId),y(i,x(r.finishedAt),"history-modal-cell-date"),y(i,r.opponentSummary||"—","history-modal-cell-opponents"),y(i,q(r.result),`history-modal-cell-result history-modal-cell-result-${r.result}`),y(i,$(r.finalScore),"history-modal-cell-score");const u=document.createElement("td");u.className="history-modal-cell-actions";const d=document.createElement("button");d.type="button",d.className="btn btn-sm btn-info history-modal-replay",d.textContent="🎞",d.title="Watch replay",d.setAttribute("aria-label",`Watch replay for game ${r.gameId.slice(0,8)}`),d.addEventListener("click",()=>{D(r.gameId)}),u.appendChild(d),i.appendChild(u),s.appendChild(i)}),t.appendChild(s),t}async function D(t){if(t!=="")try{const e=await E(()=>import("./replay-launcher.689f03f3.js"),[],import.meta.url);f(),e.openReplayForGame(t)}catch(e){}}function j(t){const e=t.slice(),a=o.sortAsc?1:-1;return e.sort((n,s)=>{switch(o.sort){case"date":{const c=n.finishedAt===null?0:Date.parse(n.finishedAt),r=s.finishedAt===null?0:Date.parse(s.finishedAt);return a*(c-r)}case"result":return a*n.result.localeCompare(s.result);case"score":return a*(n.finalScore-s.finalScore);default:return 0}}),e}function y(t,e,a){const n=document.createElement("td");n.className=`history-modal-cell ${a}`,n.textContent=e,t.appendChild(n)}function x(t){if(t===null||t==="")return"—";const e=Date.parse(t);return isNaN(e)?t:new Date(e).toLocaleDateString()}function $(t){if(!isFinite(t))return"—";const e=Math.round(t);return e>0?`+${e}`:String(e)}function q(t){return t.length===0?t:t.charAt(0).toUpperCase()+t.slice(1)}async function M(){const t=A(o.format);if(t===null){o.error="Sign in to download your match history.",m();return}o.error=null,m();try{const e=await fetch(t,{credentials:"same-origin",headers:{Accept:o.format==="csv"?"text/csv":"application/json"}});if(e.status===404){o.endpointAvailable=!1,m();return}if(!e.ok)throw new Error(`HTTP ${e.status}`);o.endpointAvailable=!0;const a=await e.blob(),n=window.URL.createObjectURL(a),s=document.createElement("a");s.href=n,s.download=_(o.format),document.body.appendChild(s),s.click(),s.remove(),window.setTimeout(()=>window.URL.revokeObjectURL(n),5e3)}catch(e){o.error=e.message,m()}}function _(t){return`mahjong-history-${new Date().toISOString().slice(0,10)}.${t}`}export{f as closeHistoryModal,k as installHistoryModal,C as openHistoryModal};
