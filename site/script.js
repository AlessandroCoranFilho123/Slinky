const toggle = document.querySelector('.theme-toggle');
function lerTemaSalvo() {
  try { return localStorage.getItem('slinky-site-theme'); } catch { return null; }
}
const savedTheme = lerTemaSalvo();

function setTheme(dark) {
  document.body.classList.toggle('dark', dark);
  toggle?.setAttribute('aria-pressed', String(dark));
  if (toggle) toggle.querySelector('span:first-child').textContent = dark ? '☾' : '☼';
}

setTheme(savedTheme === 'dark');
toggle?.addEventListener('click', () => {
  const dark = !document.body.classList.contains('dark');
  setTheme(dark);
  try { localStorage.setItem('slinky-site-theme', dark ? 'dark' : 'light'); } catch { }
});
