import { useState } from "react";
import { useDispatch } from "react-redux";
import { useSearchParams } from "react-router-dom";
import { Card } from "@mui/material";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import CatalogFilters from "./components/CatalogFilters";
import CatalogTable from "./components/CatalogTable";
import CatalogItemDialog from "./components/CatalogItemDialog";
import { useAsync } from "../../../hooks/useAsync";
import { useAction } from "../../../hooks/useAction";
import { configurationService } from "../../../services/configurationService";
import { configurationAdminService } from "../../../services/operationsService";
import { notify } from "../../../store/notifySlice";
import { errorMessage } from "../../../utils/format";

const PAGE_SIZE = 10;

// A-02 (UC-37) with the A-03 add/edit dialog and the active toggle. Filters and page live in the query string.
export default function Catalog() {
  const dispatch = useDispatch();
  const [params, setParams] = useSearchParams();
  const { data, loading, error, reload, setData } = useAsync(() => configurationService.getCatalog());
  const [editing, setEditing] = useState(undefined);
  const { busy, run } = useAction();

  const filters = { query: params.get("q") ?? "", category: params.get("category") ?? "", status: params.get("status") ?? "" };
  const setParam = (key, value) => setParams((p) => {
    const next = new URLSearchParams(p);
    if (value) next.set(key, value); else next.delete(key);
    if (key !== "page") next.delete("page");
    return next;
  }, { replace: true });

  const replace = (saved) => setData((items) => (items ?? []).some((i) => i.id === saved.id)
    ? items.map((i) => (i.id === saved.id ? saved : i)) : [...(items ?? []), saved]);
  const save = async (item) => {
    try {
      const saved = editing ? await configurationAdminService.updateCatalogItem(editing.id, item) : await configurationService.addCatalogItem(item);
      replace(saved);
      setEditing(undefined);
      dispatch(notify(`Đã lưu dịch vụ ${saved.name}.`));
      return null;
    } catch (e) {
      return errorMessage(e);
    }
  };
  const toggle = async (item) => {
    const saved = await run(() => configurationAdminService.setCatalogStatus(item.id, !item.active),
      item.active ? `Đã ngừng dùng ${item.name}.` : `Đã dùng lại ${item.name}.`);
    if (saved) replace(saved);
  };

  const query = filters.query.trim().toLowerCase();
  const items = (data ?? []).filter((i) => (!query || i.name.toLowerCase().includes(query))
    && (!filters.category || i.category === filters.category)
    && (!filters.status || i.active === (filters.status === "active")))
    .sort((a, b) => a.id - b.id);
  const pages = Math.max(1, Math.ceil(items.length / PAGE_SIZE));
  const page = Math.min(pages, Math.max(1, Number(params.get("page")) || 1));

  return (
    <>
      <PageHeader code="A-02" title="Danh mục dịch vụ chuẩn" subtitle="Dịch vụ dùng để lập đặc tả tiệc cưới và tính khung giá." />
      <Card>
        <CatalogFilters filters={filters} onChange={setParam} onAdd={() => setEditing(null)} />
        <DataState loading={loading} error={error} onRetry={reload}>
          {data && (
            <CatalogTable items={items.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE)} total={items.length} page={page} pages={pages}
              pageSize={PAGE_SIZE} onPage={(p) => setParam("page", p > 1 ? String(p) : "")} busy={busy} onEdit={setEditing} onToggle={toggle} />
          )}
        </DataState>
      </Card>
      <CatalogItemDialog open={editing !== undefined} initial={editing} onClose={() => setEditing(undefined)} onSave={save} />
    </>
  );
}
