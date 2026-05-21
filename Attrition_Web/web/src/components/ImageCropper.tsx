"use client";

import React, { useRef, useState, useEffect, useCallback } from "react";
import { X, ZoomIn } from "lucide-react";
import styles from "./ImageCropper.module.css";

interface ImageCropperProps {
  /** The image file selected by the user */
  file: File;
  /** Shape of crop area */
  shape: "circle" | "rectangle";
  /** Aspect ratio for the crop area (width / height). Default 1 for circle, 4.5 for rectangle */
  aspectRatio?: number;
  /** Size of the crop viewport in pixels (the shorter dimension). Default 280 */
  cropSize?: number;
  /** Called with the cropped image Blob when user confirms */
  onCrop: (blob: Blob) => void;
  /** Called when user cancels */
  onCancel: () => void;
}

const VIEWPORT_WIDTH = 480;
const VIEWPORT_HEIGHT = 400;

export default function ImageCropper({
  file,
  shape,
  aspectRatio,
  cropSize = 280,
  onCrop,
  onCancel,
}: ImageCropperProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [imageEl, setImageEl] = useState<HTMLImageElement | null>(null);
  const [zoom, setZoom] = useState(1);
  const [offset, setOffset] = useState({ x: 0, y: 0 });
  const [isDragging, setIsDragging] = useState(false);
  const dragStart = useRef({ x: 0, y: 0, ox: 0, oy: 0 });

  // Compute crop region dimensions
  const ratio = aspectRatio ?? (shape === "circle" ? 1 : 4.5);
  let cropW: number, cropH: number;
  if (ratio >= 1) {
    cropW = cropSize;
    cropH = cropSize / ratio;
  } else {
    cropH = cropSize;
    cropW = cropSize * ratio;
  }

  // Constrain to viewport
  if (cropW > VIEWPORT_WIDTH - 40) {
    cropW = VIEWPORT_WIDTH - 40;
    cropH = cropW / ratio;
  }
  if (cropH > VIEWPORT_HEIGHT - 40) {
    cropH = VIEWPORT_HEIGHT - 40;
    cropW = cropH * ratio;
  }

  const cropX = (VIEWPORT_WIDTH - cropW) / 2;
  const cropY = (VIEWPORT_HEIGHT - cropH) / 2;

  // Load image from file
  useEffect(() => {
    const url = URL.createObjectURL(file);
    const img = new Image();
    img.onload = () => {
      setImageEl(img);
      // Calculate initial zoom to fit image so crop area is filled
      const scaleX = cropW / img.naturalWidth;
      const scaleY = cropH / img.naturalHeight;
      const initialZoom = Math.max(scaleX, scaleY);
      setZoom(initialZoom);
      setOffset({ x: 0, y: 0 });
    };
    img.src = url;
    return () => URL.revokeObjectURL(url);
  }, [file, cropW, cropH]);

  // Render canvas
  const renderCanvas = useCallback(() => {
    const canvas = canvasRef.current;
    const ctx = canvas?.getContext("2d");
    if (!canvas || !ctx || !imageEl) return;

    canvas.width = VIEWPORT_WIDTH;
    canvas.height = VIEWPORT_HEIGHT;

    ctx.clearRect(0, 0, VIEWPORT_WIDTH, VIEWPORT_HEIGHT);

    // Draw image centered with zoom and offset
    const imgW = imageEl.naturalWidth * zoom;
    const imgH = imageEl.naturalHeight * zoom;
    const drawX = (VIEWPORT_WIDTH - imgW) / 2 + offset.x;
    const drawY = (VIEWPORT_HEIGHT - imgH) / 2 + offset.y;

    ctx.drawImage(imageEl, drawX, drawY, imgW, imgH);
  }, [imageEl, zoom, offset]);

  useEffect(() => {
    renderCanvas();
  }, [renderCanvas]);

  // Zoom range: ensure the image always covers the crop area
  const getMinZoom = () => {
    if (!imageEl) return 0.1;
    const scaleX = cropW / imageEl.naturalWidth;
    const scaleY = cropH / imageEl.naturalHeight;
    return Math.max(scaleX, scaleY);
  };

  const handleZoomChange = (newZoom: number) => {
    const minZoom = getMinZoom();
    const clampedZoom = Math.max(minZoom, Math.min(5, newZoom));
    setZoom(clampedZoom);
    // Clamp offset so image always covers crop area
    clampOffset(offset, clampedZoom);
  };

  const clampOffset = (off: { x: number; y: number }, z: number) => {
    if (!imageEl) return;
    const imgW = imageEl.naturalWidth * z;
    const imgH = imageEl.naturalHeight * z;

    // Image edges must cover crop rectangle
    // drawX = (VP_W - imgW)/2 + ox => left edge of image
    // Need: drawX <= cropX  => ox <= cropX - (VP_W - imgW)/2
    // Need: drawX + imgW >= cropX + cropW  => ox >= cropX + cropW - (VP_W - imgW)/2 - imgW
    const centerX = (VIEWPORT_WIDTH - imgW) / 2;
    const centerY = (VIEWPORT_HEIGHT - imgH) / 2;

    const maxOx = cropX - centerX;
    const minOx = (cropX + cropW) - (centerX + imgW);
    const maxOy = cropY - centerY;
    const minOy = (cropY + cropH) - (centerY + imgH);

    const clampedX = Math.max(minOx, Math.min(maxOx, off.x));
    const clampedY = Math.max(minOy, Math.min(maxOy, off.y));

    if (clampedX !== off.x || clampedY !== off.y) {
      setOffset({ x: clampedX, y: clampedY });
    }
  };

  // Pan handlers
  const handleMouseDown = (e: React.MouseEvent) => {
    e.preventDefault();
    setIsDragging(true);
    dragStart.current = { x: e.clientX, y: e.clientY, ox: offset.x, oy: offset.y };
  };

  useEffect(() => {
    if (!isDragging) return;

    const handleMouseMove = (e: MouseEvent) => {
      const dx = e.clientX - dragStart.current.x;
      const dy = e.clientY - dragStart.current.y;
      const newOffset = { x: dragStart.current.ox + dx, y: dragStart.current.oy + dy };

      // Clamp so image covers crop area
      if (imageEl) {
        const imgW = imageEl.naturalWidth * zoom;
        const imgH = imageEl.naturalHeight * zoom;
        const centerX = (VIEWPORT_WIDTH - imgW) / 2;
        const centerY = (VIEWPORT_HEIGHT - imgH) / 2;

        const maxOx = cropX - centerX;
        const minOx = (cropX + cropW) - (centerX + imgW);
        const maxOy = cropY - centerY;
        const minOy = (cropY + cropH) - (centerY + imgH);

        newOffset.x = Math.max(minOx, Math.min(maxOx, newOffset.x));
        newOffset.y = Math.max(minOy, Math.min(maxOy, newOffset.y));
      }

      setOffset(newOffset);
    };

    const handleMouseUp = () => {
      setIsDragging(false);
    };

    document.addEventListener("mousemove", handleMouseMove);
    document.addEventListener("mouseup", handleMouseUp);
    return () => {
      document.removeEventListener("mousemove", handleMouseMove);
      document.removeEventListener("mouseup", handleMouseUp);
    };
  }, [isDragging, zoom, imageEl, cropX, cropY, cropW, cropH]);

  // Scroll to zoom
  const handleWheel = (e: React.WheelEvent) => {
    e.preventDefault();
    const delta = e.deltaY > 0 ? -0.05 : 0.05;
    handleZoomChange(zoom + delta);
  };

  // Crop and export
  const handleCrop = () => {
    if (!imageEl) return;

    // Figure out what part of the source image is in the crop rectangle
    const imgW = imageEl.naturalWidth * zoom;
    const imgH = imageEl.naturalHeight * zoom;
    const drawX = (VIEWPORT_WIDTH - imgW) / 2 + offset.x;
    const drawY = (VIEWPORT_HEIGHT - imgH) / 2 + offset.y;

    // The crop rectangle in canvas coordinates maps to source image coordinates
    const srcX = (cropX - drawX) / zoom;
    const srcY = (cropY - drawY) / zoom;
    const srcW = cropW / zoom;
    const srcH = cropH / zoom;

    // Output size — use source pixel dimensions for quality, capped at 1200px
    const maxOut = 1200;
    let outW = srcW;
    let outH = srcH;
    if (outW > maxOut) {
      outH = (maxOut / outW) * outH;
      outW = maxOut;
    }

    const outCanvas = document.createElement("canvas");
    outCanvas.width = Math.round(outW);
    outCanvas.height = Math.round(outH);
    const outCtx = outCanvas.getContext("2d")!;

    // For circle shape, clip to circle
    if (shape === "circle") {
      outCtx.beginPath();
      outCtx.arc(outW / 2, outH / 2, Math.min(outW, outH) / 2, 0, Math.PI * 2);
      outCtx.closePath();
      outCtx.clip();
    }

    outCtx.drawImage(
      imageEl,
      srcX, srcY, srcW, srcH,
      0, 0, outW, outH
    );

    outCanvas.toBlob(
      (blob) => {
        if (blob) onCrop(blob);
      },
      "image/png",
      0.95
    );
  };

  return (
    <div className={styles.cropperOverlay} onClick={onCancel}>
      <div className={styles.cropperModal} onClick={(e) => e.stopPropagation()}>
        {/* Header */}
        <div className={styles.cropperHeader}>
          <span className={styles.cropperTitle}>
            Crop {shape === "circle" ? "Avatar" : "Background"}
          </span>
          <button className={styles.cropperClose} onClick={onCancel} title="Cancel">
            <X size={18} />
          </button>
        </div>

        {/* Canvas area */}
        <div
          className={styles.cropperBody}
          style={{ width: VIEWPORT_WIDTH, height: VIEWPORT_HEIGHT }}
          onMouseDown={handleMouseDown}
          onWheel={handleWheel}
        >
          <canvas
            ref={canvasRef}
            className={styles.cropperCanvas}
            width={VIEWPORT_WIDTH}
            height={VIEWPORT_HEIGHT}
          />
          {/* Crop guide overlay */}
          <div className={styles.cropGuide}>
            {shape === "circle" ? (
              <div
                className={styles.cropGuideCircle}
                style={{
                  left: cropX,
                  top: cropY,
                  width: cropW,
                  height: cropH,
                }}
              />
            ) : (
              <div
                className={styles.cropGuideRect}
                style={{
                  left: cropX,
                  top: cropY,
                  width: cropW,
                  height: cropH,
                }}
              />
            )}
          </div>
        </div>

        {/* Zoom controls */}
        <div className={styles.cropperControls}>
          <ZoomIn size={14} style={{ color: "var(--text-muted)" }} />
          <input
            type="range"
            className={styles.zoomSlider}
            min={getMinZoom()}
            max={5}
            step={0.01}
            value={zoom}
            onChange={(e) => handleZoomChange(parseFloat(e.target.value))}
          />
          <span className={styles.zoomLabel}>{Math.round(zoom * 100)}%</span>
        </div>

        {/* Footer */}
        <div className={styles.cropperFooter}>
          <button className="btn btn-secondary btn-sm" onClick={onCancel}>
            Cancel
          </button>
          <button className="btn btn-primary btn-sm" onClick={handleCrop}>
            Apply Crop
          </button>
        </div>
      </div>
    </div>
  );
}
