import { Link as RouterLink } from "react-router-dom";
import { useDispatch } from "react-redux";
import { Alert, Button } from "@mui/material";
import AttachFileIcon from "@mui/icons-material/AttachFile";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import ContractInfo from "./components/ContractInfo";
import SpecificationGroups from "./components/SpecificationGroups";
import { useCustomerProject } from "../../../hooks/useCustomerProject";
import { loadCustomerProject } from "../../../store/customerSlice";
import { PATHS } from "../../../routes/paths";

// C-02: read-only for the customer; changes go through change requests.
export default function Contract() {
  const dispatch = useDispatch();
  const { loading, error, project } = useCustomerProject();
  return (
    <>
      <PageHeader code="C-02" title="Hợp đồng và đặc tả chi tiết" subtitle="Chế độ chỉ đọc."
        actions={<Button variant="outlined" startIcon={<AttachFileIcon />} component={RouterLink} to={PATHS.customer.addenda}>Phụ lục hợp đồng</Button>} />
      <DataState loading={loading} error={error} onRetry={() => dispatch(loadCustomerProject())}>
        {project ? (
          <>
            <ContractInfo project={project} />
            <SpecificationGroups items={project.specification ?? []} baseValue={project.baseValue} />
          </>
        ) : <Alert severity="info">Tài khoản hiện chưa có hồ sơ hợp đồng để hiển thị.</Alert>}
      </DataState>
    </>
  );
}
