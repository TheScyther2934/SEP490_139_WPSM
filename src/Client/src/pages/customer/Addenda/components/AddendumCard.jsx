import { Button, Card, CardContent, Chip, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from "@mui/material";
import DownloadIcon from "@mui/icons-material/Download";
import { dateTimeVi, money, signedMoney } from "../../../../utils/format";

// One signed addendum (from an approved CR) with its line changes.
export default function AddendumCard({ addendum, onDownload, downloading }) {
  return (
    <Card>
      <CardContent>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={1} sx={{ justifyContent: "space-between", mb: 1 }}>
          <div>
            <Typography variant="h6">{addendum.code}</Typography>
            <Typography variant="body2" color="text.secondary">
              Từ yêu cầu {addendum.crCode} · {addendum.signedAt ? `Ký lúc ${dateTimeVi(addendum.signedAt)}` : "Chưa ký"}
            </Typography>
          </div>
          <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
            <Chip size="small" label={addendum.status} />
            <Chip size="small" color={addendum.totalVariance >= 0 ? "secondary" : "success"} label={signedMoney(addendum.totalVariance)} />
          </Stack>
        </Stack>
        <TableContainer>
          <Table size="small">
            <TableHead>
              <TableRow><TableCell>Hạng mục</TableCell><TableCell align="right">Cũ</TableCell><TableCell align="right">Mới</TableCell><TableCell align="right">Chênh lệch</TableCell></TableRow>
            </TableHead>
            <TableBody>
              {addendum.items.map((i) => (
                <TableRow key={i.itemName}>
                  <TableCell>{i.itemName}</TableCell>
                  <TableCell align="right">{i.oldQuantity} × {money(i.oldUnitPrice)}</TableCell>
                  <TableCell align="right">{i.newQuantity} × {money(i.newUnitPrice)}</TableCell>
                  <TableCell align="right">{signedMoney(i.deltaAmount)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
        {addendum.fileId && (
          <Button sx={{ mt: 1 }} startIcon={<DownloadIcon />} onClick={() => onDownload(addendum.fileId)} disabled={downloading} data-action="download-addendum">
            Tải bản phụ lục
          </Button>
        )}
      </CardContent>
    </Card>
  );
}
