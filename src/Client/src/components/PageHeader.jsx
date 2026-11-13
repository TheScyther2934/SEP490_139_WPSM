import { Box, Chip, Stack, Typography } from "@mui/material";
import { useTheme } from "@mui/material/styles";

// Title row of every screen; the screen code keeps the link to the SRS visible.
// Themes with `wpsm.plainHeader` (Admin) get a large plain title with the code as a small caption.
export default function PageHeader({ code, title, subtitle, actions }) {
  const plain = useTheme().wpsm?.plainHeader;
  return (
    <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 3, justifyContent: "space-between", alignItems: { sm: plain ? "center" : "flex-end" } }}>
      <Box>
        {code && !plain && <Chip label={`Mã: ${code}`} size="small" variant="outlined" sx={{ mb: 1 }} />}
        <Typography variant={plain ? "h4" : "h5"} component="h1" color={plain ? "text.primary" : "primary"}>{title}</Typography>
        {(subtitle || (plain && code)) && (
          <Typography color="text.secondary" sx={{ mt: 0.5 }}>
            {plain && code && <Box component="span" sx={{ fontWeight: 600, mr: subtitle ? 1 : 0 }}>{code}{subtitle ? " ·" : ""}</Box>}
            {subtitle}
          </Typography>
        )}
      </Box>
      {actions && <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", gap: 1 }}>{actions}</Stack>}
    </Stack>
  );
}
