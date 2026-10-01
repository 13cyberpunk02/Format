(function () {
  var theme = 'dark';
  try {
    var saved = localStorage.getItem('format-theme');
    if (saved === 'light' || saved === 'dark') {
      theme = saved;
    } else if (window.matchMedia('(prefers-color-scheme: light)').matches) {
      theme = 'light';
    }
  } catch (e) {}
  document.documentElement.setAttribute('data-theme', theme);
})();
