"use strict";
const notice = document.getElementById("notice");
const history = document.getElementById("history");
const refresh = document.getElementById("refresh");
function add(parent, tag, value) {
    const node = document.createElement(tag);
    node.textContent = value;
    parent.appendChild(node);
    return node;
}
async function load() {
    refresh.disabled = true;
    history.replaceChildren();
    notice.textContent = "Consultando...";
    try {
        const response = await fetch("/api/ana/advisories", { credentials: "same-origin", cache: "no-store" });
        if (response.status === 401 || response.status === 403) {
            notice.textContent = "Acesso restrito ao perfil de revisao de risco.";
            return;
        }
        if (!response.ok) throw new Error("Unavailable");
        const rows = await response.json();
        if (!Array.isArray(rows)) throw new Error("Invalid response");
        for (const row of rows) {
            const card = document.createElement("article");
            card.className = "panel";
            const statuses = {
                RISK_REVIEW_REQUIRED: "Revisao de risco necessaria",
                INCOMPLETE_REVIEW: "Evidencias incompletas",
                HUMAN_REVIEW_REQUIRED: "Revisao humana necessaria"
            };
            const heading = add(card, "h2", Object.hasOwn(statuses, row.status) ? statuses[row.status] : "Status desconhecido");
            heading.className = row.status === "RISK_REVIEW_REQUIRED" ? "ana-critical" : "ana-warning";
            add(card, "p", new Date(row.createdUtc).toLocaleString("pt-BR"));
            add(card, "p", "Justificativas");
            const reasons = add(card, "ul", "");
            for (const reason of row.reasons || []) add(reasons, "li", reason);
            add(card, "p", "Evidencias");
            const evidence = add(card, "ul", "");
            for (const id of row.evidenceIds || []) add(evidence, "li", id);
            add(card, "p", "Negociacao nao autorizada");
            history.appendChild(card);
        }
        notice.textContent = rows.length ? rows.length + " parecer(es) recentes." : "Nenhum parecer registrado.";
    } catch {
        history.replaceChildren();
        notice.textContent = "Historico indisponivel. Tente novamente.";
    } finally { refresh.disabled = false; }
}
refresh.addEventListener("click", load);
load();
