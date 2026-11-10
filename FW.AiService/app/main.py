import base64
from fastapi import FastAPI, File, UploadFile, Form, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from app.schemas.face_dto import FaceExtractResponse, FaceVerifyResponse, FaceVerifyRequest
from app.services.face_engine import face_engine
import json

app = FastAPI(
    title="FlexiWork AI Face Recognition Service",
    description="Microservice trích xuất và đối soát sinh trắc học Face ID (InsightFace / ArcFace 512D)",
    version="1.0.0"
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

@app.get("/health")
def health_check():
    return {
        "status": "online",
        "service": "FlexiWork AI Face Service",
        "model": "InsightFace (ArcFace 512-dim)"
    }

@app.post("/api/face/extract", response_model=FaceExtractResponse)
async def extract_face_embedding(file: UploadFile = File(...)):
    """
    API 1: Trích xuất vector đặc trưng 512 chiều từ file ảnh chụp chân dung
    """
    contents = await file.read()
    image = face_engine.read_image_from_bytes(contents)
    if image is None:
        return FaceExtractResponse(success=False, message="File tải lên không phải là ảnh hợp lệ")

    detected, bbox, embedding, msg = face_engine.extract_embedding(image)
    return FaceExtractResponse(
        success=detected,
        message=msg,
        face_detected=detected,
        bounding_box=bbox,
        embedding=embedding
    )

@app.post("/api/face/verify", response_model=FaceVerifyResponse)
async def verify_face(
    target_embedding_json: str = Form(..., description="Vector mẫu lưu trong DB dạng chuỗi JSON"),
    file: UploadFile = File(..., description="Ảnh chụp lúc Check-in")
):
    """
    API 2: Đối soát ảnh vừa chụp với vector mẫu lấy từ CSDL (Phục vụ chấm công Check-in)
    """
    try:
        target_vec = json.loads(target_embedding_json)
    except Exception:
        return FaceVerifyResponse(success=False, message="target_embedding_json không đúng định dạng JSON")

    contents = await file.read()
    image = face_engine.read_image_from_bytes(contents)
    if image is None:
        return FaceVerifyResponse(success=False, message="Ảnh check-in không hợp lệ")

    detected, bbox, checkin_embedding, msg = face_engine.extract_embedding(image)
    if not detected or checkin_embedding is None:
        return FaceVerifyResponse(success=False, message=f"Thất bại: {msg}", is_matched=False, confidence=0.0)

    # Tính độ tương đồng Cosine
    similarity = face_engine.calculate_cosine_similarity(target_vec, checkin_embedding)
    
    threshold = 0.60 # Ngưỡng 60% cho ArcFace / MobileFaceNet L2-normalized
    is_matched = similarity >= threshold

    return FaceVerifyResponse(
        success=True,
        message="Đối soát khuôn mặt hoàn tất",
        is_matched=is_matched,
        confidence=round(similarity, 4),
        threshold=threshold
    )

if __name__ == "__main__":
    import uvicorn
    uvicorn.run("main:app", host="0.0.0.0", port=8000, reload=True)
