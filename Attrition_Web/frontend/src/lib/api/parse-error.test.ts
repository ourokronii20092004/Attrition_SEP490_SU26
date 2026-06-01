import { describe, it, expect } from "vitest";
import { parseApiError } from "./parse-error";
import { ApiError } from "./client";

describe("parseApiError", () => {
  it("returns the fallback for non-ApiError values", () => {
    expect(parseApiError(new Error("x"), "fallback")).toBe("fallback");
    expect(parseApiError("string", "fallback")).toBe("fallback");
    expect(parseApiError(null, "fallback")).toBe("fallback");
  });

  it("extracts the ApiResponse envelope error", () => {
    const e = new ApiError(400, JSON.stringify({ success: false, error: "Username taken." }));
    expect(parseApiError(e)).toBe("Username taken.");
  });

  it("flattens ASP.NET validation errors", () => {
    const e = new ApiError(400, JSON.stringify({
      title: "One or more validation errors occurred.",
      errors: { Password: ["Too short.", "Needs a digit."], Email: ["Invalid."] },
    }));
    // All field messages joined — order follows Object.values insertion order.
    expect(parseApiError(e)).toBe("Too short. Needs a digit. Invalid.");
  });

  it("prefers validation errors over the title", () => {
    const e = new ApiError(400, JSON.stringify({ title: "generic", errors: { X: ["specific"] } }));
    expect(parseApiError(e)).toBe("specific");
  });

  it("falls back to message/title when there is no error field", () => {
    expect(parseApiError(new ApiError(500, JSON.stringify({ message: "boom" })))).toBe("boom");
    expect(parseApiError(new ApiError(500, JSON.stringify({ title: "t" })))).toBe("t");
  });

  it("uses the raw body when it is not JSON", () => {
    expect(parseApiError(new ApiError(502, "Bad Gateway"))).toBe("Bad Gateway");
  });

  it("uses the provided fallback when body is empty and unparseable", () => {
    expect(parseApiError(new ApiError(500, ""), "fb")).toBe("fb");
  });
});
