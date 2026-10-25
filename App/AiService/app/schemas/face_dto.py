from pydantic import BaseModel, Field
from typing import List, Optional

class FaceExtractResponse(BaseModel):
    success: bool
    message: str
    face_detected: bool = False
    bounding_box: Optional[List[int]] = None
    embedding: Optional[List[float]] = None

class FaceVerifyRequest(BaseModel):
    target_embedding: List[float] = Field(..., description="Vector đặc trưng 512 chiều lưu trong CSDL")
    image_base64: Optional[str] = Field(None, description="Ảnh checkin dạng Base64 (nếu không gửi file)")

class FaceVerifyResponse(BaseModel):
    success: bool
    message: str
    is_matched: bool = False
    confidence: float = 0.0
    threshold: float = 0.60
