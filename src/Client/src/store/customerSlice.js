import { createAsyncThunk, createSlice } from "@reduxjs/toolkit";
import { projectService } from "../services/projectService";
import { errorMessage } from "../utils/format";

// The customer's own project, loaded once by CustomerLayout (GBR-12: the API picks it from the token).
export const loadCustomerProject = createAsyncThunk("customer/load", async (_, { rejectWithValue }) => {
  try {
    return await projectService.getSnapshot();
  } catch (error) {
    return rejectWithValue(errorMessage(error));
  }
});

const customerSlice = createSlice({
  name: "customer",
  initialState: { status: "idle", error: null, snapshot: null },
  reducers: {},
  extraReducers: (builder) => {
    builder
      .addCase(loadCustomerProject.pending, (state) => { state.status = "loading"; state.error = null; })
      .addCase(loadCustomerProject.fulfilled, (state, { payload }) => { state.status = "ready"; state.snapshot = payload; })
      .addCase(loadCustomerProject.rejected, (state, { payload }) => { state.status = "error"; state.error = payload; });
  },
});

export const selectCustomerState = (state) => state.customer;
export default customerSlice.reducer;
