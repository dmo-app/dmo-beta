// Keep the draft while closing/reopening the native disclosure.
// Without JavaScript the summary still opens and closes the form.
(() => {
  const disclosure = document.getElementById('bq-tool-create');
  if (!disclosure) return;
  disclosure.addEventListener('toggle', () => {
    if (disclosure.open) document.getElementById('Draft_Reference')?.focus();
  });
})();
