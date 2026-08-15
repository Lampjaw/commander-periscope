/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./Components/**/*.razor", "./wwwroot/**/*.html"],
  // deduction-grid-N (N = a map's sectors-per-side) is built via Razor string interpolation
  // (RadioOperatorPanel.razor), so it never appears as a literal token for Tailwind's static
  // content scanner to find — without this it gets purged from the compiled CSS entirely.
  safelist: [{ pattern: /deduction-grid-[2-8]/ }],
  theme: {
    extend: {},
  },
  plugins: [],
};
