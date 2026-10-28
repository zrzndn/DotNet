"use strict";

(() => {
  const storageKey = "opshub-static-demo-v1";
  const priorities = ["Critical", "High", "Medium", "Low"];
  const statuses = ["Open", "In Progress", "Resolved"];
  const categories = ["Hardware", "Network", "Software", "Access and Accounts"];
  const priorityRank = new Map(priorities.map((value, index) => [value, index]));
  const $ = (selector) => document.querySelector(selector);
  const form = $("#ticket-form");
  const ticketDialog = $("#ticket-dialog");
  const detailDialog = $("#detail-dialog");
  let toastTimer;
  let tickets = loadTickets();

  function dateOffset(days) {
    const date = new Date();
    date.setDate(date.getDate() + days);
    return date.toISOString().slice(0, 10);
  }

  function sampleTickets() {
    return [
      { id: 1048, title: "Laptop won't connect to office Wi-Fi", description: "Connection drops every few minutes after the latest system update.", requester: "Jordan Lee", assignee: "M. Chen", category: "Network", priority: "High", status: "In Progress", created: dateOffset(-1), due: dateOffset(1) },
      { id: 1047, title: "Request access to design workspace", description: "Please add me to the shared product design workspace.", requester: "Taylor Morgan", assignee: "A. Patel", category: "Access and Accounts", priority: "Medium", status: "Open", created: dateOffset(-2), due: dateOffset(3) },
      { id: 1046, title: "External monitor not detected", description: "The second monitor is no longer detected when connected to the docking station.", requester: "Casey Rivera", assignee: "M. Chen", category: "Hardware", priority: "Critical", status: "Open", created: dateOffset(-3), due: dateOffset(-1) },
      { id: 1045, title: "Install approved PDF editor", description: "Need the approved PDF editor installed for document review.", requester: "Sam Kim", assignee: "R. Brooks", category: "Software", priority: "Low", status: "Resolved", created: dateOffset(-5), due: dateOffset(-2) },
      { id: 1044, title: "Shared drive permission issue", description: "The Finance shared folder is visible but I cannot open files.", requester: "Alex Parker", assignee: "A. Patel", category: "Access and Accounts", priority: "High", status: "In Progress", created: dateOffset(-6), due: dateOffset(-2) },
      { id: 1043, title: "Keyboard replacement", description: "Several keys on the current keyboard are sticking.", requester: "Jamie Wilson", assignee: "", category: "Hardware", priority: "Low", status: "Open", created: dateOffset(-8), due: dateOffset(5) },
      { id: 1042, title: "VPN sign-in fails after password change", description: "VPN client rejects the new password even though web sign-in works.", requester: "Morgan Davis", assignee: "R. Brooks", category: "Network", priority: "Medium", status: "Resolved", created: dateOffset(-10), due: dateOffset(-6) },
      { id: 1041, title: "Request spreadsheet application update", description: "Please update the spreadsheet application to the latest approved version.", requester: "Riley Thompson", assignee: "M. Chen", category: "Software", priority: "Medium", status: "Open", created: dateOffset(-12), due: dateOffset(2) },
      { id: 1040, title: "New employee account setup", description: "Prepare standard account access for a new team member.", requester: "Drew Anderson", assignee: "A. Patel", category: "Access and Accounts", priority: "Critical", status: "Resolved", created: dateOffset(-15), due: dateOffset(-11) }
    ];
  }

  function loadTickets() {
    try {
      const saved = localStorage.getItem(storageKey);
      if (saved === null) return sampleTickets();
      const parsed = JSON.parse(saved);
      if (!Array.isArray(parsed) || parsed.some((ticket) => !isValidTicket(ticket))) {
        throw new Error("Saved demo tickets have an invalid format.");
      }
      return parsed;
    } catch (error) {
      console.error("Could not load OpsHub demo data.", error);
      showToast("Browser storage could not be read. The demo is using fresh sample data.", true);
      return sampleTickets();
    }
  }

  function isValidTicket(ticket) {
    return ticket && Number.isInteger(ticket.id) && typeof ticket.title === "string" &&
      typeof ticket.description === "string" && typeof ticket.requester === "string" &&
      typeof ticket.assignee === "string" && categories.includes(ticket.category) &&
      priorities.includes(ticket.priority) && statuses.includes(ticket.status) &&
      typeof ticket.created === "string" && typeof ticket.due === "string";
  }

  function saveTickets() {
    try {
      localStorage.setItem(storageKey, JSON.stringify(tickets));
      return true;
    } catch (error) {
      console.error("Could not save OpsHub demo data.", error);
      showToast("Changes are visible for this visit but could not be saved in browser storage.", true);
      return false;
    }
  }

  function escapeHtml(value) {
    return String(value).replace(/[&<>"']/g, (character) => ({
      "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
    })[character]);
  }

  function formatDate(value) {
    if (!value) return "No due date";
    const date = new Date(`${value}T00:00:00`);
    return Number.isNaN(date.getTime()) ? "No due date" : new Intl.DateTimeFormat(undefined, { month: "short", day: "numeric", year: "numeric" }).format(date);
  }

  function statusClass(status) {
    return status === "In Progress" ? "progress" : status.toLowerCase();
  }

  function badgeClass(value) {
    return value === "In Progress" ? "status-progress" : `${value === "Open" || value === "Resolved" ? "status" : "priority"}-${value.toLowerCase()}`;
  }

  function showToast(message, isError = false) {
    const toast = $("#toast");
    toast.textContent = message;
    toast.classList.toggle("error", isError);
    toast.classList.add("visible");
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => toast.classList.remove("visible"), 4500);
  }

  function updateSummary(container, items, labels, kind) {
    const counts = labels.map((label) => items.filter((ticket) => ticket[kind] === label).length);
    const max = Math.max(...counts, 1);
    const classes = kind === "status"
      ? ["bar-open", "bar-progress", "bar-resolved"]
      : ["bar-critical", "bar-high", "bar-medium", "bar-low"];
    container.innerHTML = labels.map((label, index) => `
      <div class="summary-row">
        <div class="summary-label"><span>${escapeHtml(label)}</span><strong>${counts[index]}</strong></div>
        <div class="summary-track" role="progressbar" aria-label="${escapeHtml(label)} tickets" aria-valuenow="${counts[index]}" aria-valuemin="0" aria-valuemax="${max}">
          <span class="${classes[index]}" style="width:${Math.round(counts[index] / max * 100)}%"></span>
        </div>
      </div>`).join("");
  }

  function filteredTickets() {
    const query = $("#search").value.trim().toLocaleLowerCase();
    const status = $("#filter-status").value;
    const priority = $("#filter-priority").value;
    const category = $("#filter-category").value;
    const results = tickets.filter((ticket) => {
      const text = [ticket.title, ticket.description, ticket.requester, ticket.assignee, ticket.category, String(ticket.id)].join(" ").toLocaleLowerCase();
      return (!query || text.includes(query)) && (!status || ticket.status === status) &&
        (!priority || ticket.priority === priority) && (!category || ticket.category === category);
    });
    const sort = $("#sort-order").value;
    results.sort((a, b) => sort === "oldest"
      ? a.created.localeCompare(b.created) || a.id - b.id
      : sort === "priority"
        ? priorityRank.get(a.priority) - priorityRank.get(b.priority) || b.created.localeCompare(a.created)
        : b.created.localeCompare(a.created) || b.id - a.id);
    return results;
  }

  function render() {
    const unresolved = tickets.filter((ticket) => ticket.status !== "Resolved");
    $("#metric-total").textContent = tickets.length;
    $("#metric-open").textContent = tickets.filter((ticket) => ticket.status === "Open").length;
    $("#metric-progress").textContent = tickets.filter((ticket) => ticket.status === "In Progress").length;
    $("#metric-resolved").textContent = tickets.filter((ticket) => ticket.status === "Resolved").length;
    $("#metric-urgent").textContent = unresolved.filter((ticket) => ["Critical", "High"].includes(ticket.priority)).length;
    $("#nav-count").textContent = tickets.length;
    updateSummary($("#status-summary"), tickets, statuses, "status");
    updateSummary($("#priority-summary"), unresolved, priorities, "priority");

    const visible = filteredTickets();
    $("#ticket-count").textContent = tickets.length;
    $("#results-label").textContent = `Showing ${visible.length} of ${tickets.length} tickets`;
    $("#empty-state").hidden = visible.length > 0;
    $(".table-wrap").hidden = visible.length === 0;
    $("#ticket-rows").innerHTML = visible.map((ticket) => `
      <tr>
        <td><a class="ticket-link" href="#ticket-${ticket.id}" data-action="details" data-id="${ticket.id}">#${ticket.id} · ${escapeHtml(ticket.title)}</a><span class="ticket-subtitle">${escapeHtml(ticket.category)}</span></td>
        <td>${escapeHtml(ticket.requester)}</td>
        <td>${formatDate(ticket.created)}</td>
        <td>${ticket.due ? formatDate(ticket.due) : '<span class="muted">No due date</span>'}</td>
        <td><span class="badge ${badgeClass(ticket.priority)}">${escapeHtml(ticket.priority)}</span></td>
        <td><label class="visually-hidden" for="status-${ticket.id}">Status for ticket ${ticket.id}</label><select class="status-select status-${statusClass(ticket.status)}" id="status-${ticket.id}" data-action="status" data-id="${ticket.id}" aria-label="Status for ticket ${ticket.id}">${statuses.map((status) => `<option${ticket.status === status ? " selected" : ""}>${escapeHtml(status)}</option>`).join("")}</select></td>
        <td><div class="row-actions"><button class="text-button" type="button" data-action="details" data-id="${ticket.id}">View</button><button class="text-button" type="button" data-action="edit" data-id="${ticket.id}">Edit</button><button class="text-button delete" type="button" data-action="delete" data-id="${ticket.id}">Delete</button></div></td>
      </tr>`).join("");
  }

  function openForm(ticket) {
    form.reset();
    $("#ticket-id").value = ticket ? ticket.id : "";
    $("#dialog-title").textContent = ticket ? `Edit ticket #${ticket.id}` : "Create a ticket";
    if (ticket) {
      for (const [name, value] of Object.entries({
        title: ticket.title, description: ticket.description, requester: ticket.requester,
        assignee: ticket.assignee, category: ticket.category, priority: ticket.priority,
        status: ticket.status, due: ticket.due
      })) form.elements.namedItem(name).value = value;
    } else {
      form.elements.namedItem("status").value = "Open";
    }
    ticketDialog.showModal();
  }

  function showDetails(id) {
    const ticket = tickets.find((item) => item.id === id);
    if (!ticket) return;
    $("#detail-title").textContent = `#${ticket.id} · ${ticket.title}`;
    $("#detail-content").innerHTML = `
      <div class="detail-grid">
        <div class="detail-item"><span>Status</span><strong><span class="badge ${badgeClass(ticket.status)}">${escapeHtml(ticket.status)}</span></strong></div>
        <div class="detail-item"><span>Priority</span><strong><span class="badge ${badgeClass(ticket.priority)}">${escapeHtml(ticket.priority)}</span></strong></div>
        <div class="detail-item"><span>Requester</span><strong>${escapeHtml(ticket.requester)}</strong></div>
        <div class="detail-item"><span>Assigned technician</span><strong>${escapeHtml(ticket.assignee || "Unassigned")}</strong></div>
        <div class="detail-item"><span>Category</span><strong>${escapeHtml(ticket.category)}</strong></div>
        <div class="detail-item"><span>Due date</span><strong>${escapeHtml(formatDate(ticket.due))}</strong></div>
        <div class="detail-item"><span>Created</span><strong>${escapeHtml(formatDate(ticket.created))}</strong></div>
      </div>
      <div><p class="eyebrow">DESCRIPTION</p><p class="detail-description">${escapeHtml(ticket.description)}</p></div>
      <div class="detail-actions"><button class="button button-secondary" type="button" data-detail-action="edit" data-id="${ticket.id}">Edit ticket</button></div>`;
    detailDialog.showModal();
  }

  function csvCell(value) {
    let text = String(value ?? "");
    if (/^[\s]*[=+\-@]/.test(text)) text = `'${text}`;
    return `"${text.replace(/"/g, '""')}"`;
  }

  $("#new-ticket").addEventListener("click", () => openForm(null));
  $("#close-dialog").addEventListener("click", () => ticketDialog.close());
  $("#cancel-dialog").addEventListener("click", () => ticketDialog.close());
  $("#close-detail").addEventListener("click", () => detailDialog.close());
  form.addEventListener("submit", (event) => {
    event.preventDefault();
    if (!form.reportValidity()) return;
    const data = new FormData(form);
    const currentId = Number($("#ticket-id").value);
    const current = tickets.find((ticket) => ticket.id === currentId);
    const ticket = {
      id: current ? current.id : Math.max(1000, ...tickets.map((item) => item.id)) + 1,
      title: String(data.get("title")).trim(),
      description: String(data.get("description")).trim(),
      requester: String(data.get("requester")).trim(),
      assignee: String(data.get("assignee")).trim(),
      category: String(data.get("category")),
      priority: String(data.get("priority")),
      status: String(data.get("status")),
      created: current ? current.created : dateOffset(0),
      due: String(data.get("due"))
    };
    if (!ticket.title || !ticket.description || !ticket.requester) return;
    if (current) tickets = tickets.map((item) => item.id === current.id ? ticket : item);
    else tickets = [ticket, ...tickets];
    saveTickets();
    ticketDialog.close();
    render();
    showToast(current ? "Ticket updated in this browser." : "Ticket added to this browser demo.");
  });

  $("#ticket-rows").addEventListener("click", (event) => {
    const button = event.target.closest("[data-action]");
    if (!button) return;
    const id = Number(button.dataset.id);
    const action = button.dataset.action;
    if (action === "details") {
      event.preventDefault();
      showDetails(id);
    } else if (action === "edit") {
      const ticket = tickets.find((item) => item.id === id);
      if (ticket) openForm(ticket);
    } else if (action === "delete") {
      const ticket = tickets.find((item) => item.id === id);
      if (ticket && confirm(`Delete sample ticket #${ticket.id}?`)) {
        tickets = tickets.filter((item) => item.id !== id);
        saveTickets();
        render();
        showToast(`Ticket #${id} deleted from this browser.`);
      }
    }
  });

  $("#ticket-rows").addEventListener("change", (event) => {
    if (event.target.dataset.action !== "status") return;
    const id = Number(event.target.dataset.id);
    tickets = tickets.map((ticket) => ticket.id === id ? { ...ticket, status: event.target.value } : ticket);
    saveTickets();
    render();
    showToast(`Ticket #${id} status updated.`);
  });

  $("#detail-content").addEventListener("click", (event) => {
    const button = event.target.closest("[data-detail-action='edit']");
    if (!button) return;
    const ticket = tickets.find((item) => item.id === Number(button.dataset.id));
    detailDialog.close();
    if (ticket) openForm(ticket);
  });

  for (const selector of ["#search", "#filter-status", "#filter-priority", "#filter-category", "#sort-order"]) {
    $(selector).addEventListener(selector === "#search" ? "input" : "change", render);
  }

  $("#clear-filters").addEventListener("click", () => {
    $("#search").value = "";
    $("#filter-status").value = "";
    $("#filter-priority").value = "";
    $("#filter-category").value = "";
    $("#sort-order").value = "newest";
    render();
  });

  $("#reset-demo").addEventListener("click", () => {
    if (!confirm("Reset all browser changes and restore the fictional sample tickets?")) return;
    tickets = sampleTickets();
    saveTickets();
    render();
    showToast("Sample tickets restored.");
  });

  $("#export-csv").addEventListener("click", () => {
    const rows = [
      ["Ticket", "Title", "Description", "Requester", "Assigned technician", "Category", "Priority", "Status", "Created", "Due"],
      ...filteredTickets().map((ticket) => [ticket.id, ticket.title, ticket.description, ticket.requester, ticket.assignee, ticket.category, ticket.priority, ticket.status, ticket.created, ticket.due])
    ];
    const csv = `\uFEFF${rows.map((row) => row.map(csvCell).join(",")).join("\r\n")}`;
    const url = URL.createObjectURL(new Blob([csv], { type: "text/csv;charset=utf-8" }));
    const link = document.createElement("a");
    link.href = url;
    link.download = "opshub-demo-tickets.csv";
    link.click();
    URL.revokeObjectURL(url);
    showToast("CSV exported for the current ticket filters.");
  });

  render();
})();
