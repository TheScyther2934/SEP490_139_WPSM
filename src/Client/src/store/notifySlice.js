import { createSlice } from "@reduxjs/toolkit";

// One toast at a time (MSG03 "Lưu thành công" and similar); AppSnackbar renders it.
const notifySlice = createSlice({
  name: "notify",
  initialState: { key: 0, message: "", severity: "success", open: false },
  reducers: {
    notify: (state, { payload }) => {
      const { message, severity = "success" } = typeof payload === "string" ? { message: payload } : payload;
      state.key += 1;
      state.message = message;
      state.severity = severity;
      state.open = true;
    },
    closeNotify: (state) => { state.open = false; },
  },
});

export const { notify, closeNotify } = notifySlice.actions;
export default notifySlice.reducer;
