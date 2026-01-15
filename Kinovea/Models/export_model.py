# SwingForge Lite - YOLOv8 Pose Model Export Script
# 
# This script exports the YOLOv8-pose model to ONNX format for use with SwingForge Lite.
# 
# Requirements:
#   pip install ultralytics
#
# Usage:
#   python export_model.py
#
# This will download yolov8n-pose.pt and export it to yolov8n-pose.onnx in the current directory.

from ultralytics import YOLO

def main():
    print("SwingForge Lite - YOLOv8 Pose Model Export")
    print("=" * 50)
    
    # Load the YOLOv8n-pose model (nano version for speed)
    print("\nLoading YOLOv8n-pose model...")
    model = YOLO("yolov8n-pose.pt")
    
    # Export to ONNX format
    print("\nExporting to ONNX format...")
    model.export(
        format="onnx",
        imgsz=640,
        simplify=True,
        opset=12,
        dynamic=False
    )
    
    print("\nExport complete!")
    print("Model saved as: yolov8n-pose.onnx")
    print("\nPlace this file in the Models folder of SwingForge Lite.")

if __name__ == "__main__":
    main()
