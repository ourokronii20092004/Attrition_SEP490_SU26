"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { Modal } from "@/components/ui/modal";
import { Button } from "@/components/ui/button";

/**
 * QOLF-3: dependency-free image cropper. User picks a file, frames it (drag to pan, slider to
 * zoom) inside a fixed viewport, and we render the visible region to a canvas → a cropped File
 * that the existing upload endpoints accept unchanged. `aspect` controls the frame (1 = square
 * avatar, e.g. 16/9 for a cover). `round` just styles the overlay (output stays rectangular).
 */
type Props = {
  file: File;
  aspect?: number;
  round?: boolean;
  onCancel: () => void;
  onCropped: (file: File) => void;
};

const VIEWPORT = 320; // px, the longer edge of the crop frame

export function ImageCropper({ file, aspect = 1, round = false, onCancel, onCropped }: Props) {
  const [imgUrl, setImgUrl] = useState<string>("");
  const [img, setImg] = useState<HTMLImageElement | null>(null);
  const [zoom, setZoom] = useState(1);
  const [offset, setOffset] = useState({ x: 0, y: 0 });
  const [busy, setBusy] = useState(false);
  const drag = useRef<{ x: number; y: number; ox: number; oy: number } | null>(null);

  const frameW = aspect >= 1 ? VIEWPORT : VIEWPORT * aspect;
  const frameH = aspect >= 1 ? VIEWPORT / aspect : VIEWPORT;

  // Load the picked file into an Image element (and clean up the object URL).
  useEffect(() => {
    const url = URL.createObjectURL(file);
    setImgUrl(url);
    const image = new Image();
    image.onload = () => setImg(image);
    image.src = url;
    return () => URL.revokeObjectURL(url);
  }, [file]);

  // Base scale: smallest scale that still covers the frame (so there's never a gap).
  const baseScale = img ? Math.max(frameW / img.width, frameH / img.height) : 1;
  const scale = baseScale * zoom;

  // Reset pan when a new image loads.
  useEffect(() => { setOffset({ x: 0, y: 0 }); setZoom(1); }, [img]);

  // Clamp the offset so the (scaled) image always covers the frame.
  const clamp = useCallback((x: number, y: number) => {
    if (!img) return { x, y };
    const sw = img.width * scale, sh = img.height * scale;
    const maxX = Math.max(0, (sw - frameW) / 2);
    const maxY = Math.max(0, (sh - frameH) / 2);
    return {
      x: Math.min(maxX, Math.max(-maxX, x)),
      y: Math.min(maxY, Math.max(-maxY, y)),
    };
  }, [img, scale, frameW, frameH]);

  useEffect(() => { setOffset((o) => clamp(o.x, o.y)); }, [scale, clamp]);

  const onPointerDown = (e: React.PointerEvent) => {
    drag.current = { x: e.clientX, y: e.clientY, ox: offset.x, oy: offset.y };
    (e.target as HTMLElement).setPointerCapture(e.pointerId);
  };
  const onPointerMove = (e: React.PointerEvent) => {
    if (!drag.current) return;
    const dx = e.clientX - drag.current.x;
    const dy = e.clientY - drag.current.y;
    setOffset(clamp(drag.current.ox + dx, drag.current.oy + dy));
  };
  const onPointerUp = () => { drag.current = null; };

  const doCrop = async () => {
    if (!img) return;
    setBusy(true);
    try {
      // Map the visible frame back to source-image pixels.
      const canvas = document.createElement("canvas");
      // Cap output so avatars/covers don't balloon; keep frame aspect.
      const maxOut = 1024;
      const outScale = Math.min(1, maxOut / Math.max(frameW, frameH));
      canvas.width = Math.round(frameW * outScale);
      canvas.height = Math.round(frameH * outScale);
      const ctx = canvas.getContext("2d");
      if (!ctx) throw new Error("no ctx");

      // Top-left of the frame in source pixels.
      const sx = (img.width * scale / 2 - frameW / 2 - offset.x) / scale;
      const sy = (img.height * scale / 2 - frameH / 2 - offset.y) / scale;
      ctx.drawImage(img, sx, sy, frameW / scale, frameH / scale, 0, 0, canvas.width, canvas.height);

      const blob: Blob | null = await new Promise((res) => canvas.toBlob(res, "image/jpeg", 0.9));
      if (!blob) throw new Error("crop failed");
      const cropped = new File([blob], file.name.replace(/\.\w+$/, "") + "-cropped.jpg", { type: "image/jpeg" });
      onCropped(cropped);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open onClose={onCancel} title="Frame your image" size="sm">
      <div className="flex flex-col items-center">
        <div
          className="relative cursor-grab touch-none overflow-hidden bg-surface-2 active:cursor-grabbing"
          style={{ width: frameW, height: frameH, borderRadius: round ? "9999px" : "0.5rem" }}
          onPointerDown={onPointerDown}
          onPointerMove={onPointerMove}
          onPointerUp={onPointerUp}
          onPointerCancel={onPointerUp}
        >
          {imgUrl && (
            <img
              src={imgUrl}
              alt=""
              draggable={false}
              className="pointer-events-none absolute left-1/2 top-1/2 max-w-none select-none"
              style={{
                width: img ? img.width * scale : undefined,
                height: img ? img.height * scale : undefined,
                transform: `translate(calc(-50% + ${offset.x}px), calc(-50% + ${offset.y}px))`,
              }}
            />
          )}
          <div className="pointer-events-none absolute inset-0 ring-1 ring-inset ring-white/20" style={{ borderRadius: round ? "9999px" : "0.5rem" }} />
        </div>

        <div className="mt-4 flex w-full items-center gap-3">
          <span className="text-xs text-fg-subtle">Zoom</span>
          <input
            type="range" min={1} max={4} step={0.01} value={zoom}
            onChange={(e) => setZoom(Number(e.target.value))}
            className="flex-1 accent-[var(--color-accent)]"
          />
        </div>

        <div className="mt-4 flex w-full justify-end gap-2">
          <Button variant="secondary" onClick={onCancel}>Cancel</Button>
          <Button onClick={doCrop} loading={busy} disabled={!img}>Apply & upload</Button>
        </div>
      </div>
    </Modal>
  );
}
