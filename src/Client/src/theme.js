import { createTheme } from "@mui/material/styles";

// Colours follow the Razor screens (wpsm-theme.js) so both UIs look alike while they run side by side.
const theme = createTheme({
  palette: {
    primary: { main: "#6d1634", light: "#8b2e4a", dark: "#3f0018", contrastText: "#ffffff" },
    secondary: { main: "#765a26", light: "#fed798", contrastText: "#ffffff" },
    error: { main: "#ba1a1a" },
    background: { default: "#fff8f5", paper: "#ffffff" },
    text: { primary: "#1e1b19", secondary: "#554245" },
    divider: "#dac0c4",
  },
  typography: {
    fontFamily: '"Be Vietnam Pro", "Segoe UI", sans-serif',
    h4: { fontWeight: 700 },
    h5: { fontWeight: 700 },
    h6: { fontWeight: 600 },
    button: { textTransform: "none", fontWeight: 600 },
  },
  shape: { borderRadius: 10 },
  components: {
    MuiCard: { defaultProps: { variant: "outlined" } },
    MuiTableCell: { styleOverrides: { head: { fontWeight: 600, color: "#554245" } } },
  },
});

export default theme;
