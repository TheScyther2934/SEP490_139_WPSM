import { useState } from "react";
import { useDispatch, useSelector } from "react-redux";
import { useSearchParams } from "react-router-dom";
import { Card } from "@mui/material";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import AccountFilters from "./components/AccountFilters";
import AccountTable from "./components/AccountTable";
import AccountDialog from "./components/AccountDialog";
import AccountConfirmDialog from "./components/AccountConfirmDialog";
import CredentialDialog from "./components/CredentialDialog";
import { useAsync } from "../../../hooks/useAsync";
import { useAction } from "../../../hooks/useAction";
import { adminService } from "../../../services/adminService";
import { notify } from "../../../store/notifySlice";
import { selectUser } from "../../../store/authSlice";
import { errorMessage } from "../../../utils/format";

const PAGE_SIZE = 10;

// A-19 account management (outside the SRS, agreed with the team): list, create Manager/Admin, edit, lock/unlock, reissue password.
// Accounts are never deleted (SRS business rule); Customer accounts are still created by M-03.
export default function Accounts() {
  const dispatch = useDispatch();
  const currentUserId = useSelector(selectUser)?.userId;
  const [params, setParams] = useSearchParams();
  const { data, loading, error, reload, setData } = useAsync(() => adminService.getAccounts());
  const [editing, setEditing] = useState(undefined);
  const [confirm, setConfirm] = useState(null);
  const [credential, setCredential] = useState(null);
  const { busy, run } = useAction();

  const filters = { query: params.get("q") ?? "", role: params.get("role") ?? "", status: params.get("status") ?? "" };
  const setParam = (key, value) => setParams((p) => {
    const next = new URLSearchParams(p);
    if (value) next.set(key, value); else next.delete(key);
    if (key !== "page") next.delete("page");
    return next;
  }, { replace: true });

  const replace = (saved) => setData((items) => (items ?? []).some((i) => i.id === saved.id)
    ? items.map((i) => (i.id === saved.id ? saved : i)) : [...(items ?? []), saved]);

  const save = async (form) => {
    try {
      if (editing) {
        const saved = await adminService.updateAccount(editing.id, form);
        replace(saved);
        dispatch(notify(`Đã cập nhật tài khoản ${saved.email}.`));
      } else {
        const created = await adminService.createAccount(form);
        replace(created.account);
        setCredential({ account: created.account, password: created.temporaryPassword, reset: false });
      }
      setEditing(undefined);
      return null;
    } catch (e) {
      return errorMessage(e);
    }
  };

  const confirmAction = async () => {
    const { kind, account } = confirm;
    if (kind === "status") {
      const saved = await run(() => adminService.setAccountStatus(account.id, !account.active),
        account.active ? `Đã khóa tài khoản ${account.email}.` : `Đã mở khóa tài khoản ${account.email}.`);
      if (saved) replace(saved);
    } else {
      const result = await run(() => adminService.resetAccountPassword(account.id));
      if (result) {
        replace(result.account);
        setCredential({ account: result.account, password: result.temporaryPassword, reset: true });
      }
    }
    setConfirm(null);
  };

  const query = filters.query.trim().toLowerCase();
  const items = (data ?? []).filter((a) => (!query || a.fullName.toLowerCase().includes(query) || a.email.toLowerCase().includes(query) || a.phone.includes(query))
    && (!filters.role || a.role === filters.role)
    && (!filters.status || a.active === (filters.status === "active")))
    .sort((a, b) => a.id - b.id);
  const pages = Math.max(1, Math.ceil(items.length / PAGE_SIZE));
  const page = Math.min(pages, Math.max(1, Number(params.get("page")) || 1));

  return (
    <>
      <PageHeader code="A-19" title="Quản lý tài khoản" subtitle="Tạo tài khoản Manager/Admin, sửa thông tin, khóa hoặc mở khóa và cấp lại mật khẩu." />
      <Card>
        <AccountFilters filters={filters} onChange={setParam} onAdd={() => setEditing(null)} />
        <DataState loading={loading} error={error} onRetry={reload}>
          {data && (
            <AccountTable items={items.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE)} total={items.length} page={page} pages={pages}
              pageSize={PAGE_SIZE} onPage={(p) => setParam("page", p > 1 ? String(p) : "")} busy={busy} currentUserId={currentUserId}
              onEdit={setEditing} onReset={(account) => setConfirm({ kind: "reset", account })} onToggle={(account) => setConfirm({ kind: "status", account })} />
          )}
        </DataState>
      </Card>
      <AccountDialog open={editing !== undefined} initial={editing} self={editing?.id === currentUserId} onClose={() => setEditing(undefined)} onSave={save} />
      <AccountConfirmDialog confirm={confirm} busy={busy} onClose={() => setConfirm(null)} onConfirm={confirmAction} />
      <CredentialDialog credential={credential} onClose={() => setCredential(null)} />
    </>
  );
}
