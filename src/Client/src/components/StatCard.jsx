import { Card, CardActionArea, CardContent, Typography } from "@mui/material";
import { Link as RouterLink } from "react-router-dom";

// KPI tile; when `to` is given the whole tile opens the related list.
export default function StatCard({ label, value, hint, to, tone = "primary", children }) {
  const body = (
    <CardContent>
      <Typography variant="body2" color="text.secondary">{label}</Typography>
      <Typography variant="h5" color={tone} sx={{ mt: 0.5 }}>{value}</Typography>
      {hint && <Typography variant="caption" color="text.secondary" component="p">{hint}</Typography>}
      {children}
    </CardContent>
  );
  return <Card sx={{ height: "100%" }}>{to ? <CardActionArea component={RouterLink} to={to} sx={{ height: "100%" }}>{body}</CardActionArea> : body}</Card>;
}
