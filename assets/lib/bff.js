/**
 * Cookie-session BFF calls — never attach Bearer tokens from the browser.
 * @param {string} path absolute path under /bff
 * @param {RequestInit} [init]
 */
export async function bffFetch(path, init = {}) {
  const headers = new Headers(init.headers);
  if (!headers.has("Accept")) {
    headers.set("Accept", "application/json");
  }

  const response = await fetch(path, {
    ...init,
    credentials: "same-origin",
    headers,
  });

  return response;
}

/**
 * @param {string} path
 * @param {unknown} [body]
 */
export async function bffJson(path, body) {
  const init =
    body === undefined
      ? {}
      : {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(body),
        };

  const response = await bffFetch(path, init);
  const text = await response.text();
  const data = text ? JSON.parse(text) : null;

  if (!response.ok) {
    const err = new Error(data?.error ?? data?.title ?? response.statusText);
    err.status = response.status;
    err.body = data;
    throw err;
  }

  return data;
}
