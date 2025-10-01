(function ensureBrowserEnvironment() {
  if (typeof window === "undefined") {
    return;
  }

  var existingProcess = window.process;
  if (existingProcess && typeof existingProcess === "object") {
    var processTag = String(existingProcess);
    if (processTag === "[object process]") {
      try {
        delete window.process;
      } catch (error) {
        window.process = undefined;
      }
    }
  }

  if (typeof window.require === "function") {
    try {
      delete window.require;
    } catch (error) {
      window.require = undefined;
    }
  }
})();
