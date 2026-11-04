import { useState } from "react";
import { useDispatch } from "react-redux";
import { Button } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import CatalogFilters from "./components/CatalogFilters";
import CatalogTable from "./components/CatalogTable";
import CatalogItemDialog from "./components/CatalogItemDialog";
import { useAsync } from "../../../hooks/useAsync";
import { configurationService } from "../../../services/configurationService";
import { notify } from "../../../store/notifySlice";
import { errorMessage } from "../../../utils/format";

// A-02 with the A-03 add dialog. Edit and active toggle wait for their API.
export default function Catalog() {
  const dispatch = useDispatch();
  const { data, loading, error, reload, setData } = useAsync(() => configurationService.getCatalog());
  const [filters, setFilters] = useState({ query: "", category: "", status: "" });
  const [dialogOpen, setDialogOpen] = useState(false);

  const save = async (item) => {
    try {
      const saved = await configurationService.addCatalogItem(item);
      setData((items) => [...(items ?? []), saved]);
      setDialogOpen(false);
      dispatch(notify(`Đã lưu dịch vụ ${saved.name}.`));
      return null;
    } catch (e) {
      return errorMessage(e);
    }
  };

  const query = filters.query.trim().toLowerCase();
  const items = (data ?? []).filter((i) => (!query || i.name.toLowerCase().includes(query))
    && (!filters.category || i.category === filters.category)
    && (!filters.status || i.active === (filters.status === "active")));

  return (
    <>
      <PageHeader code="A-02" title="Danh mục dịch vụ chuẩn" subtitle="Dịch vụ dùng để lập đặc tả tiệc cưới và tính khung giá."
        actions={<Button variant="contained" startIcon={<AddIcon />} onClick={() => setDialogOpen(true)}>Thêm dịch vụ</Button>} />
      <CatalogFilters filters={filters} onChange={(key, value) => setFilters((f) => ({ ...f, [key]: value }))} />
      <DataState loading={loading} error={error} onRetry={reload}>
        {data && <CatalogTable items={items} />}
      </DataState>
      <CatalogItemDialog open={dialogOpen} onClose={() => setDialogOpen(false)} onSave={save} />
    </>
  );
}
