import sys
import json
from PIL import Image
import pytesseract
import imagehash

def process_image(image_path):
    """
    Processes an image to extract OCR text and generate a perceptual hash.
    Args:
        image_path (str): The path to the image file.
    Returns:
        dict: A dictionary containing the OCR text and pHash.
              Returns error information if processing fails.
    """
    results = {}
    try:
        img = Image.open(image_path)

        # Perform OCR
        try:
            ocr_text = pytesseract.image_to_string(img)
            results['ocr_text'] = ocr_text.strip()
        except pytesseract.TesseractError as e:
            results['ocr_error'] = str(e)
            results['ocr_text'] = None
        except Exception as e: # Catch other potential errors during OCR
            results['ocr_error'] = f"An unexpected error occurred during OCR: {str(e)}"
            results['ocr_text'] = None


        # Generate perceptual hash
        try:
            phash = imagehash.phash(img)
            results['phash'] = str(phash)
        except Exception as e: # Catch potential errors during hashing
            results['phash_error'] = f"An unexpected error occurred during pHash generation: {str(e)}"
            results['phash'] = None

    except FileNotFoundError:
        return {'error': f"Image file not found: {image_path}"}
    except Exception as e:
        return {'error': f"Failed to open or process image {image_path}: {str(e)}"}

    return results

if __name__ == '__main__':
    if len(sys.argv) != 2:
        print(json.dumps({'error': "Usage: python process_image.py <image_path>"}))
        sys.exit(1)

    image_file_path = sys.argv[1]
    output = process_image(image_file_path)
    print(json.dumps(output))
