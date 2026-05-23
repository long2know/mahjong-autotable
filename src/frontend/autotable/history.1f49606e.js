var e=globalThis.parcelRequire7651;(0,e.register)("6QNuX",function(t,o){Object.defineProperty(t.exports,"installHistoryModal",{get:()=>function e(){r.installed||(function(){let t=document.querySelector("#profile-recent-games")?.closest(".profile-page-section");if(null===t||null!==t.querySelector("#profile-history-link"))return;let o=t.querySelector(".profile-page-section-title"),l=document.createElement("button");if(l.id="profile-history-link",l.type="button",l.className="btn btn-info btn-sm profile-history-link",l.setAttribute("data-testid","profile-history-link"),l.textContent="\uD83D\uDCE5 Match history",l.setAttribute("aria-label","Open match history export modal"),l.addEventListener("click",()=>(function(){r.installed||e();let t=n("history-modal");null!==t&&(r.open=!0,t.classList.add("history-modal-open"),t.setAttribute("aria-hidden","false"),(0,a.showEl)(t),d(),window.setTimeout(()=>{let e=n("history-modal-close");e?.focus()},50))})()),null!==o&&null!==o.parentElement){let e=document.createElement("div");e.className="profile-page-section-title-row",o.parentElement.insertBefore(e,o),e.appendChild(o),e.appendChild(l)}else t.insertBefore(l,t.firstChild)}(),function(){if(null!==n("history-modal"))return;let e=document.createElement("div");e.id="history-modal",e.className="history-modal",e.setAttribute("role","dialog"),e.setAttribute("aria-modal","true"),e.setAttribute("aria-label","Match history export"),e.setAttribute("aria-hidden","true"),e.setAttribute("data-testid","history-modal"),e.hidden=!0,e.innerHTML=`
    <div class="history-modal-backdrop" data-history-dismiss></div>
    <div class="history-modal-shell" role="document">
      <header class="history-modal-header">
        <h2 class="history-modal-title">\u{1F4E5} Match history</h2>
        <button id="history-modal-close" type="button"
                class="history-modal-close"
                data-testid="history-modal-close"
                aria-label="Close match history">\xd7</button>
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
            <option value="custom">Custom range\u{2026}</option>
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
          \u{2B07} Download
        </button>
      </section>
      <section class="history-modal-status">
        <div id="history-modal-error"
             class="history-modal-error" hidden aria-live="polite"></div>
        <div id="history-modal-loading"
             class="history-modal-loading" hidden aria-live="polite">
          Loading recent matches\u{2026}
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
  `,document.body.appendChild(e),e.querySelectorAll("[data-history-dismiss]").forEach(e=>{e.addEventListener("click",()=>s())});let t=e.querySelector("#history-modal-close");t?.addEventListener("click",()=>s());let o=e.querySelector("#history-date-range");o?.addEventListener("change",()=>{r.range=o.value;let t=e.querySelector("#history-custom-range");null!==t&&(0,a.setElHidden)(t,"custom"!==r.range),d()});let l=e.querySelector("#history-date-from");l?.addEventListener("change",()=>{r.customFrom=""===l.value?null:l.value,"custom"===r.range&&d()});let i=e.querySelector("#history-date-to");i?.addEventListener("change",()=>{r.customTo=""===i.value?null:i.value,"custom"===r.range&&d()}),e.querySelectorAll('input[name="history-format"]').forEach(e=>{e.addEventListener("change",()=>{e.checked&&("json"===e.value||"csv"===e.value)&&(r.format=e.value)})});let c=e.querySelector("#history-download");c?.addEventListener("click",()=>void h()),document.addEventListener("keydown",e=>{r.open&&"Escape"===e.key&&s()})}(),r.installed=!0)},set:void 0,enumerable:!0,configurable:!0});var l=e("dCbgc"),a=e("22woc");let r={installed:!1,open:!1,range:"30",customFrom:null,customTo:null,format:"json",rows:[],loading:!1,error:null,endpointAvailable:null,sort:"date",sortAsc:!1};function n(e){return document.getElementById(e)}function s(){let e=n("history-modal");null!==e&&(r.open=!1,e.classList.remove("history-modal-open"),e.setAttribute("aria-hidden","true"),(0,a.hideEl)(e))}function i(e){let t=(0,l.getProfile)(),o=t?.playerId??"";if(""===o||"offline"===o)return null;let a=new URLSearchParams;a.set("playerId",o),a.set("format",e);let{from:n,to:s}=function(){if("custom"===r.range)return{from:r.customFrom,to:r.customTo};let e=parseInt(r.range,10);if(isNaN(e)||e<=0)return{from:null,to:null};let t=new Date;return{from:new Date(t.getTime()-864e5*e).toISOString(),to:t.toISOString()}}();return null!==n&&""!==n&&a.set("from",n),null!==s&&""!==s&&a.set("to",s),`/api/games?${a.toString()}`}async function d(){if(!r.open)return;let e=i("json");if(null===e){r.rows=[],r.error="Sign in to view your match history.",c();return}r.loading=!0,r.error=null,c();try{let t=await fetch(e,{credentials:"same-origin",headers:{Accept:"application/json"}});if(404===t.status){r.endpointAvailable=!1,r.rows=[],r.loading=!1,c();return}if(!t.ok)throw Error(`HTTP ${t.status}`);r.endpointAvailable=!0;let o=await t.json();r.rows=(function(e){let t=[];for(let o of Array.isArray(e)?e:null!==e&&"object"==typeof e&&Array.isArray(e.games)?e.games:[]){if(null===o||"object"!=typeof o)continue;let e=o,l="string"==typeof e.gameId&&""!==e.gameId?e.gameId:"string"==typeof e.id?e.id:"";if(""===l)continue;let a="string"==typeof e.finishedAt?e.finishedAt:"string"==typeof e.completedAt?e.completedAt:null,r=(()=>{let t="string"==typeof e.result?e.result.toLowerCase():"";return"win"===t||"won"===t?"win":"loss"===t||"lost"===t?"loss":"draw"===t||"washout"===t?"draw":"unknown"})(),n="number"==typeof e.finalScore?e.finalScore:"number"==typeof e.score?e.score:0,s="string"==typeof e.opponentSummary?e.opponentSummary:"string"==typeof e.summary?e.summary:"string"==typeof e.opponents?e.opponents:"";t.push({gameId:l,finishedAt:a,result:r,finalScore:n,opponentSummary:s})}return t})(o).slice(0,20)}catch(e){r.error=e.message,r.rows=[]}finally{r.loading=!1,c()}}function c(){let e=n("history-recent-table-host"),t=n("history-modal-error"),o=n("history-modal-loading"),l=n("history-modal-unavailable");if(null!==e&&(null!==t&&(null!==r.error?((0,a.showEl)(t),t.textContent=r.error):((0,a.hideEl)(t),t.textContent="")),null!==o&&(0,a.setElHidden)(o,!r.loading),null!==l&&(0,a.setElHidden)(l,!1!==r.endpointAvailable),e.replaceChildren(),!1!==r.endpointAvailable)){if(0===r.rows.length&&!r.loading){let t=document.createElement("div");t.className="history-modal-empty",t.textContent="No matches found in this range.",e.appendChild(t);return}e.appendChild(function(){let e=document.createElement("table");e.className="history-modal-table history-recent-table",e.setAttribute("data-testid","history-recent-table"),e.setAttribute("role","table");let t=document.createElement("thead"),o=document.createElement("tr");for(let e of[{key:"date",label:"Date"},{key:null,label:"Opponents"},{key:"result",label:"Result"},{key:"score",label:"Score"},{key:null,label:""}]){let t=document.createElement("th");if(t.className="history-modal-th",t.scope="col",t.textContent=e.label,null!==e.key){t.setAttribute("tabindex","0"),t.setAttribute("role","columnheader"),e.key===r.sort&&(t.classList.add("history-modal-th-active"),t.setAttribute("aria-sort",r.sortAsc?"ascending":"descending"));let o=()=>{r.sort===e.key?r.sortAsc=!r.sortAsc:(r.sort=e.key,r.sortAsc="date"!==e.key),c()};t.addEventListener("click",o),t.addEventListener("keydown",e=>{("Enter"===e.key||" "===e.key)&&(e.preventDefault(),o())})}o.appendChild(t)}t.appendChild(o),e.appendChild(t);let l=document.createElement("tbody");return(function(e){let t=e.slice(),o=r.sortAsc?1:-1;return t.sort((e,t)=>{switch(r.sort){case"date":return o*((null===e.finishedAt?0:Date.parse(e.finishedAt))-(null===t.finishedAt?0:Date.parse(t.finishedAt)));case"result":return o*e.result.localeCompare(t.result);case"score":return o*(e.finalScore-t.finalScore);default:return 0}}),t})(r.rows).forEach((e,t)=>{var o;let a=document.createElement("tr");a.className=`history-modal-row history-modal-row-${e.result}`,a.setAttribute("data-testid",`history-recent-row-${t}`),a.setAttribute("data-game-id",e.gameId),m(a,function(e){if(null===e||""===e)return"—";let t=Date.parse(e);return isNaN(t)?e:new Date(t).toLocaleDateString()}(e.finishedAt),"history-modal-cell-date"),m(a,e.opponentSummary||"—","history-modal-cell-opponents"),m(a,0===(o=e.result).length?o:o.charAt(0).toUpperCase()+o.slice(1),`history-modal-cell-result history-modal-cell-result-${e.result}`),m(a,function(e){if(!isFinite(e))return"—";let t=Math.round(e);return t>0?`+${t}`:String(t)}(e.finalScore),"history-modal-cell-score");let r=document.createElement("td");r.className="history-modal-cell-actions";let n=document.createElement("button");n.type="button",n.className="btn btn-sm btn-info history-modal-replay",n.textContent="\uD83C\uDF9E",n.title="Watch replay",n.setAttribute("aria-label",`Watch replay for game ${e.gameId.slice(0,8)}`),n.addEventListener("click",()=>{u(e.gameId)}),r.appendChild(n),a.appendChild(r),l.appendChild(a)}),e.appendChild(l),e}())}}async function u(t){if(""!==t)try{let o=await Promise.resolve(e("1oeUa"));s(),o.openReplayForGame(t)}catch{}}function m(e,t,o){let l=document.createElement("td");l.className=`history-modal-cell ${o}`,l.textContent=t,e.appendChild(l)}async function h(){let e=i(r.format);if(null===e){r.error="Sign in to download your match history.",c();return}r.error=null,c();try{let t=await fetch(e,{credentials:"same-origin",headers:{Accept:"csv"===r.format?"text/csv":"application/json"}});if(404===t.status){r.endpointAvailable=!1,c();return}if(!t.ok)throw Error(`HTTP ${t.status}`);r.endpointAvailable=!0;let o=await t.blob(),l=window.URL.createObjectURL(o),a=document.createElement("a");a.href=l,a.download=function(e){let t=new Date().toISOString().slice(0,10);return`mahjong-history-${t}.${e}`}(r.format),document.body.appendChild(a),a.click(),a.remove(),window.setTimeout(()=>window.URL.revokeObjectURL(l),5e3)}catch(e){r.error=e.message,c()}}});