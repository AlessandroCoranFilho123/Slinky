const toggle = document.querySelector('.theme-toggle');
const savedTheme = localStorage.getItem('slinky-site-theme');

function setTheme(dark) {
  document.body.classList.toggle('dark', dark);
  toggle?.setAttribute('aria-pressed', String(dark));
  if (toggle) toggle.querySelector('span:first-child').textContent = dark ? '☾' : '☼';
}

setTheme(savedTheme === 'dark');
toggle?.addEventListener('click', () => {
  const dark = !document.body.classList.contains('dark');
  setTheme(dark);
  localStorage.setItem('slinky-site-theme', dark ? 'dark' : 'light');
});
