/**
 * Step-up before bind_form save: Authenticate via BFF login (login_form),
 * then submit the Razor form with bind_form fields only.
 */
import { bffPost } from "./lib/bff.js";

const form = document.getElementById("bind-update-form");
const dialog = document.getElementById("bind-reauth-dialog");
if (!form) {
  // Page has no binding editor.
} else {
  const stepUp = form.dataset.stepUp || "none";
  const providerId = form.dataset.providerId || "";
  let steppedUp = false;

  form.addEventListener("submit", (event) => {
    if (steppedUp || stepUp === "none") {
      return;
    }

    event.preventDefault();
    if (!dialog) {
      return;
    }

    dialog.showModal();
  });

  const cancel = document.getElementById("bind-reauth-cancel");
  cancel?.addEventListener("click", () => dialog?.close());

  const reauthForm = document.getElementById("bind-reauth-form");
  const errorEl = document.getElementById("bind-reauth-error");

  reauthForm?.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (errorEl) {
      errorEl.textContent = "";
      errorEl.classList.add("hidden");
    }

    const payload = {};
    for (const input of reauthForm.querySelectorAll("input[name]")) {
      payload[input.name] = input.value;
    }

    try {
      await bffPost("/bff/auth/login", {
        provider_id: providerId,
        payload,
      });
      steppedUp = true;
      dialog?.close();
      form.requestSubmit();
    } catch (err) {
      if (errorEl) {
        errorEl.textContent = err?.body?.error || err?.message || "Authentication failed.";
        errorEl.classList.remove("hidden");
      }
    }
  });
}
