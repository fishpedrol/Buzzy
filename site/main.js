const header = document.querySelector(".site-header");
const menuButton = document.querySelector(".menu-toggle");
const navigation = document.querySelector("#navegacao");

function closeMenu({ returnFocus = false } = {}) {
  if (!header || !menuButton) return;
  header.dataset.menuOpen = "false";
  menuButton.setAttribute("aria-expanded", "false");
  menuButton.setAttribute("aria-label", "Abrir navegação");
  if (returnFocus) menuButton.focus();
}

menuButton?.addEventListener("click", () => {
  const isOpen = menuButton.getAttribute("aria-expanded") === "true";
  header.dataset.menuOpen = String(!isOpen);
  menuButton.setAttribute("aria-expanded", String(!isOpen));
  menuButton.setAttribute("aria-label", isOpen ? "Abrir navegação" : "Fechar navegação");
});

navigation?.querySelectorAll("a").forEach((link) => {
  link.addEventListener("click", () => closeMenu());
});

document.addEventListener("keydown", (event) => {
  if (event.key === "Escape" && header?.dataset.menuOpen === "true") {
    closeMenu({ returnFocus: true });
  }
});
