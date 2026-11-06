import { createTheme } from "@mui/material/styles";

// Executive look of the Admin area (mockups A-01..A-08): navy top bar, blue actions, grey page, bordered tables.
// Only AdminLayout uses it, so Manager and Customer keep theme.js.
export const NAVY = "#24324a";

const adminTheme = createTheme({
  palette: {
    primary: { main: "#1a5fc8", light: "#4f86de", dark: "#0f4596", contrastText: "#ffffff" },
    secondary: { main: NAVY, contrastText: "#ffffff" },
    error: { main: "#c62828", light: "#fdecea" },
    success: { main: "#2e7d32", light: "#e8f5e9" },
    background: { default: "#f3f5f8", paper: "#ffffff" },
    text: { primary: "#1b2430", secondary: "#5b6576" },
    divider: "#dde2ea",
  },
  typography: {
    fontFamily: '"Be Vietnam Pro", "Segoe UI", sans-serif',
    h4: { fontWeight: 700, fontSize: "1.75rem" },
    h5: { fontWeight: 700 },
    h6: { fontWeight: 600, fontSize: "1.05rem" },
    button: { textTransform: "none", fontWeight: 600 },
  },
  shape: { borderRadius: 8 },
  // Read by PageHeader: large plain title, screen code as a small caption.
  wpsm: { plainHeader: true },
  components: {
    MuiCard: { defaultProps: { variant: "outlined" }, styleOverrides: { root: { borderColor: "#dde2ea" } } },
    MuiButton: { defaultProps: { disableElevation: true } },
    MuiTableCell: {
      styleOverrides: {
        root: { borderColor: "#e6e9ef" },
        head: { fontWeight: 600, color: "#3a4556", backgroundColor: "#eef1f5", whiteSpace: "nowrap" },
      },
    },
    MuiTableRow: { styleOverrides: { root: { "&:last-child td": { borderBottom: 0 } } } },
    MuiChip: { styleOverrides: { root: { fontWeight: 600 } } },
    MuiDialogTitle: { styleOverrides: { root: { backgroundColor: "#eef1f5", borderBottom: "1px solid #dde2ea", fontWeight: 600, marginBottom: 16 } } },
    MuiDialogActions: { styleOverrides: { root: { padding: "12px 24px 20px" } } },
  },
});

export default adminTheme;
