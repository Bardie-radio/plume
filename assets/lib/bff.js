/**
 * Cookie-session BFF calls — never attach Bearer tokens from the browser.
 * @param {string} path absolute path under /bff
 * @param {{ method?: string, body?: unknown, headers?: HeadersInit }} [options]
 */
export async function bffRequest(path, options = {}) {
  const { method = "GET", body, headers: initHeaders } = options;
  const headers = new Headers(initHeaders);
  if (!headers.has("Accept")) {
    headers.set("Accept", "application/json");
  }

  /** @type {RequestInit} */
  const init = {
    method,
    credentials: "same-origin",
    headers,
  };

  if (body !== undefined) {
    headers.set("Content-Type", "application/json");
    init.body = JSON.stringify(body);
  }

  const response = await fetch(path, init);

  if (response.status === 401) {
    window.location.assign("/login");
    const err = new Error("unauthorized");
    err.status = 401;
    throw err;
  }

  const text = await response.text();
  let data = null;
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      data = { raw: text };
    }
  }

  if (!response.ok) {
    const err = new Error(data?.error ?? data?.title ?? response.statusText);
    err.status = response.status;
    err.body = data;
    throw err;
  }

  return data;
}

/** @param {string} path */
export function bffGet(path) {
  return bffRequest(path, { method: "GET" });
}

/**
 * @param {string} path
 * @param {unknown} [body] omit for empty POST (pause / skip / unpause)
 */
export function bffPost(path, body) {
  if (arguments.length < 2) {
    return bffRequest(path, { method: "POST" });
  }

  return bffRequest(path, { method: "POST", body });
}

/** @param {string} path */
export function bffDelete(path) {
  return bffRequest(path, { method: "DELETE" });
}

/** @deprecated prefer bffGet / bffPost */
export async function bffFetch(path, init = {}) {
  return fetch(path, { ...init, credentials: "same-origin" });
}

/** @deprecated prefer bffGet / bffPost */
export async function bffJson(path, body) {
  if (body === undefined) {
    return bffGet(path);
  }

  return bffPost(path, body);
}
