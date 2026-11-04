import { Accordion, AccordionDetails, AccordionSummary, Box, Card, CardContent, Stack, Typography } from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import { money } from "../../../../utils/format";

// Read-only specification grouped by category (C-02).
export default function SpecificationGroups({ items, baseValue }) {
  const groups = items.reduce((acc, item) => { (acc[item.category] ||= []).push(item); return acc; }, {});
  const total = items.reduce((sum, i) => sum + Number(i.lineTotal || 0), 0);
  return (
    <Card>
      <CardContent>
        <Typography variant="h6">Đặc tả chi tiết theo nhóm</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>{items.length} hạng mục</Typography>
        {items.length === 0 ? <Typography color="text.secondary">Chưa có hạng mục đặc tả.</Typography> : (
          Object.entries(groups).map(([category, list]) => (
            <Accordion key={category} defaultExpanded disableGutters>
              <AccordionSummary expandIcon={<ExpandMoreIcon />}>
                <Stack direction="row" sx={{ width: "100%", justifyContent: "space-between", pr: 1, flexWrap: "wrap", gap: 1 }}>
                  <Typography fontWeight={700} color="primary">{category} · {list.length} hạng mục</Typography>
                  <Typography variant="body2">Tổng nhóm: {money(list.reduce((s, i) => s + Number(i.lineTotal || 0), 0))}</Typography>
                </Stack>
              </AccordionSummary>
              <AccordionDetails>
                {list.map((item) => (
                  <Box key={item.catalogItemId} sx={{ display: "grid", gap: 1, gridTemplateColumns: { md: "3fr 1fr 1fr 1fr" }, py: 1.5, borderTop: 1, borderColor: "divider" }}>
                    <div>
                      <Typography fontWeight={600}>{item.name}</Typography>
                      <Typography variant="body2" color="text.secondary" sx={{ whiteSpace: "pre-line" }}>{item.description}</Typography>
                    </div>
                    <Typography>{item.quantity} {item.unit}</Typography>
                    <Typography color="text.secondary">{money(item.unitPrice)}</Typography>
                    <Typography fontWeight={600} color="primary" sx={{ textAlign: { md: "right" } }}>{money(item.lineTotal)}</Typography>
                  </Box>
                ))}
              </AccordionDetails>
            </Accordion>
          ))
        )}
        <Stack direction="row" spacing={3} sx={{ mt: 2, justifyContent: "flex-end", flexWrap: "wrap" }}>
          <Typography>Tổng các hạng mục: <b>{money(total)}</b></Typography>
          <Typography>Giá trị hợp đồng gốc: <b>{money(baseValue)}</b></Typography>
        </Stack>
      </CardContent>
    </Card>
  );
}
