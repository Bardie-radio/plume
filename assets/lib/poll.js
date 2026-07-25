/** MVP poll interval for queue / now-playing (plan: 2s). */
export const POLL_INTERVAL_MS = 2000;

/**
 * Repeat `tick` every `intervalMs` while the page is visible.
 * @param {() => void | Promise<void>} tick
 * @param {number} [intervalMs]
 * @returns {() => void} stop
 */
export function startPoll(tick, intervalMs = POLL_INTERVAL_MS) {
  let timer = null;
  let stopped = false;

  const run = async () => {
    if (stopped || document.visibilityState === "hidden") {
      return;
    }

    try {
      await tick();
    } catch {
      // Callers surface errors in the island UI; keep polling.
    }
  };

  const schedule = () => {
    if (stopped) {
      return;
    }

    timer = window.setTimeout(async () => {
      await run();
      schedule();
    }, intervalMs);
  };

  const onVisibility = () => {
    if (document.visibilityState === "visible") {
      void run();
    }
  };

  document.addEventListener("visibilitychange", onVisibility);
  void run();
  schedule();

  return () => {
    stopped = true;
    if (timer !== null) {
      window.clearTimeout(timer);
    }

    document.removeEventListener("visibilitychange", onVisibility);
  };
}
