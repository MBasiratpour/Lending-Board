// Lending Board frontend: plain JavaScript that talks to the REST API.

const API = "http://localhost:5082/api";

const memberSelect = document.getElementById("member-select");
const message = document.getElementById("message");

// ---------- Helpers ----------

/** Calls the API and returns the JSON answer. Throws an Error with a readable message. */
async function api(path, method = "GET", body) {
  let response;
  try {
    response = await fetch(API + path, {
      method,
      headers: body ? { "Content-Type": "application/json" } : {},
      body: body ? JSON.stringify(body) : undefined,
    });
  } catch {
    throw new Error("Cannot reach the server. Is the backend running?");
  }

  const text = await response.text();
  const data = text ? JSON.parse(text) : null;
  if (!response.ok) {
    throw new Error(data?.error ?? `Request failed (${response.status}).`);
  }
  return data;
}

/** Creates an HTML element. Text is set safely, never as HTML. */
function el(tag, className, text) {
  const element = document.createElement(tag);
  if (className) element.className = className;
  if (text !== undefined) element.textContent = text;
  return element;
}

function button(label, onClick, className) {
  const element = el("button", className, label);
  element.type = "button";
  element.addEventListener("click", () => run(onClick));
  return element;
}

/** Runs an action, shows its error if it fails, then reloads the page data. */
async function run(action) {
  message.hidden = true;
  try {
    await action();
  } catch (error) {
    message.textContent = error.message;
    message.hidden = false;
  }
  await refresh().catch(() => {});
}

function currentMemberId() {
  return Number(memberSelect.value);
}

// ---------- Members ----------

async function loadMembers() {
  const members = await api("/members");
  const remembered = localStorage.getItem("memberId");
  memberSelect.replaceChildren();
  for (const member of members) {
    const option = el("option", "", member.name);
    option.value = member.id;
    memberSelect.append(option);
  }
  if (remembered && members.some((m) => String(m.id) === remembered)) {
    memberSelect.value = remembered;
  }
}

memberSelect.addEventListener("change", () => {
  localStorage.setItem("memberId", memberSelect.value);
  run(async () => {});
});

document.getElementById("member-form").addEventListener("submit", (event) => {
  event.preventDefault();
  const input = document.getElementById("member-name");
  run(async () => {
    const member = await api("/members", "POST", { name: input.value });
    localStorage.setItem("memberId", member.id);
    input.value = "";
    await loadMembers();
  });
});

// ---------- Board ----------

const statusLabels = { Available: "Available", Requested: "Requested", Approved: "On loan" };

async function loadItems() {
  const items = await api("/items");
  const me = currentMemberId();
  const container = document.getElementById("items");
  container.replaceChildren();

  if (items.length === 0) {
    container.append(el("p", "empty", "Nothing on the board yet. Offer the first item below."));
  }

  for (const item of items) {
    const card = el("article", "card");
    card.append(el("h3", "", item.name));
    if (item.description) card.append(el("p", "", item.description));
    card.append(el("p", "meta", `${item.category} · from ${item.ownerId === me ? "you" : item.ownerName}`));
    card.append(el("span", "badge " + item.status.toLowerCase(), statusLabels[item.status]));

    const actions = el("div", "actions");
    if (item.ownerId === me) {
      actions.append(button("Remove", () => api(`/items/${item.id}?memberId=${me}`, "DELETE"), "secondary"));
    } else if (item.status === "Available") {
      actions.append(button("Ask to borrow", () => api(`/items/${item.id}/loans`, "POST", { memberId: me })));
    }
    card.append(actions);
    container.append(card);
  }
}

document.getElementById("item-form").addEventListener("submit", (event) => {
  event.preventDefault();
  const form = event.target;
  run(async () => {
    await api("/items", "POST", {
      name: document.getElementById("item-name").value,
      description: document.getElementById("item-description").value,
      category: document.getElementById("item-category").value,
      ownerId: currentMemberId(),
    });
    form.reset();
  });
});

// ---------- My loans ----------

const loanLabels = { Requested: "Waiting for answer", Approved: "On loan", Declined: "Declined", Returned: "Returned" };

function loanRow(loan, text, actionButtons) {
  const row = el("div", "row");
  const info = el("div");
  info.append(el("strong", "", loan.itemName), el("div", "meta", text));
  const actions = el("div", "actions");
  actions.append(el("span", "badge", loanLabels[loan.status]), ...actionButtons);
  row.append(info, actions);
  return row;
}

async function loadLoans() {
  const me = currentMemberId();
  const loans = await api(`/loans?memberId=${me}`);
  const lending = document.getElementById("loans-lending");
  const borrowing = document.getElementById("loans-borrowing");
  lending.replaceChildren();
  borrowing.replaceChildren();

  for (const loan of loans) {
    const act = (action) => () => api(`/loans/${loan.id}/${action}`, "POST", { memberId: me });

    if (loan.ownerId === me) {
      const buttons = [];
      if (loan.status === "Requested") {
        buttons.push(button("Approve", act("approve")), button("Decline", act("decline"), "secondary"));
      }
      if (loan.status === "Approved") {
        buttons.push(button("Got it back", act("return")));
      }
      lending.append(loanRow(loan, `${loan.borrowerName} wants to borrow it`, buttons));
    } else {
      borrowing.append(loanRow(loan, `from ${loan.ownerName}`, []));
    }
  }

  if (!lending.children.length) lending.append(el("p", "empty", "Nobody has asked for your items yet."));
  if (!borrowing.children.length) borrowing.append(el("p", "empty", "You have not asked for anything yet."));
}

// ---------- Tabs ----------

const views = { board: document.getElementById("view-board"), loans: document.getElementById("view-loans") };
const tabs = { board: document.getElementById("tab-board"), loans: document.getElementById("tab-loans") };

function showView(name) {
  for (const key of Object.keys(views)) {
    views[key].hidden = key !== name;
    tabs[key].classList.toggle("active", key === name);
  }
}
tabs.board.addEventListener("click", () => showView("board"));
tabs.loans.addEventListener("click", () => showView("loans"));

// ---------- Start ----------

async function refresh() {
  await Promise.all([loadItems(), loadLoans()]);
}

run(loadMembers);
