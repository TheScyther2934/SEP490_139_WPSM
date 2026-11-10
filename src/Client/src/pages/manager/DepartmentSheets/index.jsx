import { Link as RouterLink } from "react-router-dom";
import { Button, Card, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import DataState from "../../../components/DataState";
import FileButton from "../../../components/FileButton";
import { DEPARTMENTS } from "../Beo/departments";
import { useAsync } from "../../../hooks/useAsync";
import { useProject } from "../../../hooks/useProject";
import { beoService } from "../../../services/operationsService";
import { PATHS } from "../../../routes/paths";
import { dateTimeVi } from "../../../utils/format";

// M-11 (UC-28): department PDFs of the latest frozen version, available only after the freeze.
export default function DepartmentSheets() {
  const { project } = useProject();
  const { data: beo, loading, error, reload } = useAsync(() => beoService.get(project.id), [project.id]);
  return (
    <>
      <Stack direction="row" sx={{ mb: 2, justifyContent: "space-between", alignItems: "center" }}>
        <Typography variant="h6">File PDF các bộ phận (M-11)</Typography>
        <Button startIcon={<ArrowBackIcon />} component={RouterLink} to={PATHS.manager.beo(project.id)}>Về BEO</Button>
      </Stack>
      <DataState loading={loading} error={error} onRetry={reload} empty={!beo?.sheets.length} emptyText="Chỉ có file PDF sau khi BEO được đóng băng.">
        <Card>
          <TableContainer>
            <Table id="sheetTable">
              <TableHead><TableRow><TableCell>Bộ phận</TableCell><TableCell>Phiên bản</TableCell><TableCell>Tạo lúc</TableCell><TableCell /></TableRow></TableHead>
              <TableBody>
                {beo?.sheets.map((s) => (
                  <TableRow key={s.department}>
                    <TableCell>{DEPARTMENTS[s.department]}</TableCell>
                    <TableCell>v{beo.version}{beo.status === "Stale" ? " (lỗi thời)" : ""}</TableCell>
                    <TableCell>{dateTimeVi(s.generatedAt)}</TableCell>
                    <TableCell align="right"><FileButton fileId={s.fileId}>Tải PDF</FileButton></TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </Card>
      </DataState>
    </>
  );
}
