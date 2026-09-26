// Headset view window. Shows the "waiting" state until a picture source is wired in;
// setStream(mediaStream) is the hook for that.
const bridge = window.mm;

document.querySelectorAll('[data-win]').forEach((b) =>
  b.addEventListener('click', () => bridge?.win(b.dataset.win)));

document.querySelector('#pin').addEventListener('click', (e) => {
  const on = bridge?.togglePin();
  e.currentTarget.classList.toggle('on', !!on);
});

function setStream(stream) {
  document.querySelector('#video').srcObject = stream;
  document.body.dataset.live = stream ? 'true' : 'false';
}
window.setStream = setStream;
